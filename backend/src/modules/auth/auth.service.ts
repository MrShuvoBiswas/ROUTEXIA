import { Injectable, BadRequestException, UnauthorizedException } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import * as bcrypt from 'bcrypt';
import { UserEntity } from '../../entities/user.entity';
import { DeviceEntity } from '../../entities/device.entity';
import { SubscriptionEntity } from '../../entities/subscription.entity';
import { RelayEntity } from '../../entities/relay.entity';
import { UserHistoryEntity } from '../../entities/user-history.entity';
import { RegisterDto, LoginDto, FirebaseLoginDto } from './dto/auth.dto';
import { FirebaseAdminService } from './firebase-admin.service';

@Injectable()
export class AuthService {
  constructor(
    @InjectRepository(UserEntity)
    private userRepository: Repository<UserEntity>,
    @InjectRepository(DeviceEntity)
    private deviceRepository: Repository<DeviceEntity>,
    @InjectRepository(SubscriptionEntity)
    private subRepository: Repository<SubscriptionEntity>,
    @InjectRepository(RelayEntity)
    private relayRepository: Repository<RelayEntity>,
    @InjectRepository(UserHistoryEntity)
    private historyRepository: Repository<UserHistoryEntity>,
    private jwtService: JwtService,
    private firebaseAdmin: FirebaseAdminService,
  ) {}

  private async logHistory(userId: string, actionType: string, title: string, details: string, daysDelta = 0, actor = 'System', remark = '') {
    try {
      const history = this.historyRepository.create({
        userId,
        actionType,
        title,
        details,
        daysDelta,
        actor,
        remark,
      });
      await this.historyRepository.save(history);
    } catch { }
  }

  // ── Email/Password Register ──────────────────────────────────────────────

  async register(dto: RegisterDto) {
    const existing = await this.userRepository.findOne({ where: { email: dto.email.toLowerCase() } });
    if (existing) {
      if (existing.isDeleted) {
        throw new BadRequestException('This account was previously deleted. Please contact an administrator to restore.');
      }
      throw new BadRequestException('Email is already registered');
    }

    // Check device ban
    let device = dto.hwid ? await this.deviceRepository.findOne({ where: { hwidHash: dto.hwid } }) : null;
    if (device?.isBanned) {
      throw new UnauthorizedException(`Hardware device is banned: ${device.banReason || 'Violation of terms'}`);
    }

    const salt = await bcrypt.genSalt(10);
    const passwordHash = await bcrypt.hash(dto.password, salt);

    const user = this.userRepository.create({
      email: dto.email.toLowerCase(),
      passwordHash,
      role: 'user',
      isFirebaseUser: false,
      referralCode: 'RX-' + Math.random().toString(36).substring(2, 8).toUpperCase(),
    });
    await this.userRepository.save(user);

    // HWID Anti-Abuse Check: If this physical PC already claimed a trial, new account gets 0 trial days
    let trialDays = 4;
    let deviceClaimedByOther = false;

    if (device && device.trialClaimed) {
      trialDays = 0;
      deviceClaimedByOther = true;
    } else if (dto.hwid) {
      if (!device) {
        device = this.deviceRepository.create({
          hwidHash: dto.hwid,
          firstUserId: user.id,
          trialClaimed: true,
        });
      } else {
        device.firstUserId = user.id;
        device.trialClaimed = true;
      }
      await this.deviceRepository.save(device);
    }

    const now = new Date();
    const expiresAt = new Date(now.getTime() + trialDays * 24 * 60 * 60 * 1000);

    const subscription = this.subRepository.create({
      userId: user.id,
      hwidHash: dto.hwid || 'WEB-AUTH',
      planType: trialDays > 0 ? 'trial' : 'expired',
      status: trialDays > 0 ? 'active' : 'expired',
      startsAt: now,
      expiresAt: expiresAt,
    });
    await this.subRepository.save(subscription);

    await this.logHistory(
      user.id,
      'INITIAL_REGISTRATION',
      'Account Created',
      `Registered with email: ${user.email}`,
      0,
      'Self',
      'Initial signup',
    );

    if (trialDays > 0) {
      await this.logHistory(
        user.id,
        'TRIAL_STARTED',
        `Free Trial Activated (+${trialDays} Days)`,
        `Valid until: ${expiresAt.toLocaleDateString()}`,
        trialDays,
        'System',
        'New user welcome trial',
      );
    } else if (deviceClaimedByOther) {
      await this.logHistory(
        user.id,
        'TRIAL_DENIED_HWID',
        'Trial Blocked (Hardware Already Used)',
        'This PC has already claimed a free trial on another account.',
        0,
        'Anti-Abuse Guard',
        'Duplicate HWID detected',
      );
    }

    return this.buildAuthResponse(user, subscription, deviceClaimedByOther);
  }

  // ── Email/Password Login ─────────────────────────────────────────────────

  async login(dto: LoginDto) {
    const user = await this.userRepository.findOne({ where: { email: dto.email.toLowerCase() } });
    if (!user) {
      throw new UnauthorizedException('Invalid email or password');
    }

    if (!user.passwordHash) {
      throw new UnauthorizedException('This account uses Firebase sign-in. Please use Google or Firebase login.');
    }

    const validPassword = await bcrypt.compare(dto.password, user.passwordHash);
    if (!validPassword) {
      throw new UnauthorizedException('Invalid email or password');
    }

    if (user.isDeleted) {
      throw new UnauthorizedException('This account has been deleted by an administrator.');
    }

    if (user.isBanned) {
      throw new UnauthorizedException(`Account banned: ${user.banReason || 'Violation of terms'}`);
    }

    // Check device ban
    let device = dto.hwid ? await this.deviceRepository.findOne({ where: { hwidHash: dto.hwid } }) : null;
    if (device?.isBanned) {
      throw new UnauthorizedException(`Hardware device is banned: ${device.banReason || 'Violation of terms'}`);
    }

    let sub = await this.subRepository.findOne({
      where: { userId: user.id },
      order: { expiresAt: 'DESC' },
    });

    const now = new Date();
    let deviceClaimedByOther = false;

    if (!sub) {
      // Check if device already claimed trial
      let trialDays = 4;
      if (device && device.trialClaimed) {
        trialDays = 0;
        deviceClaimedByOther = true;
      } else if (dto.hwid) {
        if (!device) {
          device = this.deviceRepository.create({
            hwidHash: dto.hwid,
            firstUserId: user.id,
            trialClaimed: true,
          });
        } else {
          device.firstUserId = user.id;
          device.trialClaimed = true;
        }
        await this.deviceRepository.save(device);
      }

      const expiresAt = new Date(now.getTime() + trialDays * 24 * 60 * 60 * 1000);
      sub = this.subRepository.create({
        userId: user.id,
        hwidHash: dto.hwid || 'WEB-AUTH',
        planType: trialDays > 0 ? 'trial' : 'expired',
        status: trialDays > 0 ? 'active' : 'expired',
        startsAt: now,
        expiresAt,
      });
      await this.subRepository.save(sub);
    } else {
      // Anti-abuse on existing trial: if user created account elsewhere with trial, but this PC already had another account's trial
      if (sub.planType === 'trial' && sub.status === 'active' && dto.hwid) {
        if (device && device.trialClaimed && device.firstUserId && device.firstUserId !== user.id) {
          sub.status = 'expired';
          sub.planType = 'expired';
          sub.expiresAt = now;
          await this.subRepository.save(sub);
          deviceClaimedByOther = true;
        }
      }
    }

    return this.buildAuthResponse(user, sub, deviceClaimedByOther);
  }

  // ── Firebase Authentication Login ────────────────────────────────────────

  async loginWithFirebase(dto: FirebaseLoginDto) {
    // 1. Verify the Firebase ID token server-side
    const decoded = await this.firebaseAdmin.verifyIdToken(dto.id_token);

    const firebaseUid  = decoded.uid;
    const email        = (decoded.email ?? dto.email ?? '').toLowerCase();

    if (!email) {
      throw new BadRequestException('Email address is required for Firebase login.');
    }

    // Enforce email verification (Account must be activated via link)
    if (!decoded.email_verified) {
      throw new UnauthorizedException('Please verify your email before logging in. An activation link was sent to your email.');
    }

    // Check device ban
    let device = dto.hwid ? await this.deviceRepository.findOne({ where: { hwidHash: dto.hwid } }) : null;
    if (device?.isBanned) {
      throw new UnauthorizedException(`Hardware device is banned: ${device.banReason || 'Violation of terms'}`);
    }

    // 2. Find or create the user in our database
    let user = await this.userRepository.findOne({
      where: [{ firebaseUid }, { email }],
    });

    if (!user) {
      // First time Firebase login — auto-register
      user = this.userRepository.create({
        email,
        passwordHash: null,
        firebaseUid,
        isFirebaseUser: true,
        role: 'user',
        referralCode: 'RX-' + Math.random().toString(36).substring(2, 8).toUpperCase(),
      });
      await this.userRepository.save(user);
    } else {
      // If user was soft-deleted, block login
      if (user.isDeleted) {
        throw new UnauthorizedException('This account has been deleted by an administrator.');
      }
      // Update firebase_uid if not yet set (existing legacy user)
      if (!user.firebaseUid) {
        user.firebaseUid   = firebaseUid;
        user.isFirebaseUser = true;
        await this.userRepository.save(user);
      }
    }

    if (user.isBanned) {
      throw new UnauthorizedException(`Account banned: ${user.banReason || 'Violation of terms'}`);
    }

    // 3. Find or create subscription
    let sub = await this.subRepository.findOne({
      where: { userId: user.id },
      order: { expiresAt: 'DESC' },
    });

    const now = new Date();
    let deviceClaimedByOther = false;

    if (!sub) {
      // HWID anti-abuse: new device gets a trial, existing device gets 0 days
      let trialDays = 4;
      if (device && device.trialClaimed) {
        trialDays = 0;
        deviceClaimedByOther = true;
      } else if (dto.hwid) {
        if (!device) {
          device = this.deviceRepository.create({
            hwidHash: dto.hwid,
            firstUserId: user.id,
            trialClaimed: true,
          });
        } else {
          device.firstUserId = user.id;
          device.trialClaimed = true;
        }
        await this.deviceRepository.save(device);
      }

      const expiresAt = new Date(now.getTime() + trialDays * 24 * 60 * 60 * 1000);
      sub = this.subRepository.create({
        userId: user.id,
        hwidHash: dto.hwid || 'FIREBASE-AUTH',
        planType: trialDays > 0 ? 'trial' : 'expired',
        status: trialDays > 0 ? 'active' : 'expired',
        startsAt: now,
        expiresAt,
      });
      await this.subRepository.save(sub);
    } else {
      // Anti-abuse on existing trial
      if (sub.planType === 'trial' && sub.status === 'active' && dto.hwid) {
        if (device && device.trialClaimed && device.firstUserId && device.firstUserId !== user.id) {
          sub.status = 'expired';
          sub.planType = 'expired';
          sub.expiresAt = now;
          await this.subRepository.save(sub);
          deviceClaimedByOther = true;
        }
      }
    }

    return this.buildAuthResponse(user, sub, deviceClaimedByOther);
  }

  // ── Profile / Real-Time Auth Status ──────────────────────────────────────

  async getProfile(userId: string) {
    const user = await this.userRepository.findOne({ where: { id: userId } });
    if (!user || user.isDeleted) {
      throw new UnauthorizedException('Account has been deleted by an administrator.');
    }
    if (user.isBanned) {
      throw new UnauthorizedException(`Account suspended: ${user.banReason || 'Violation of terms'}`);
    }

    const sub = await this.subRepository.findOne({
      where: { userId: user.id },
      order: { expiresAt: 'DESC' },
    });

    if (!sub) {
      throw new UnauthorizedException('Subscription record not found.');
    }

    return this.buildAuthResponse(user, sub);
  }

  // ── Shared Response Builder ──────────────────────────────────────────────

  private async buildAuthResponse(user: UserEntity, sub: SubscriptionEntity, deviceClaimedByOther = false) {
    const payload = { sub: user.id, email: user.email, role: user.role };
    const token = this.jwtService.sign(payload);

    const now = new Date();
    const daysLeft = Math.max(0, Math.ceil((new Date(sub.expiresAt).getTime() - now.getTime()) / (1000 * 60 * 60 * 24)));
    const canConnect = !user.isBanned && sub.status === 'active' && new Date(sub.expiresAt) > now;

    let subMessage = 'Subscription inactive';
    if (canConnect) {
      subMessage = sub.planType === 'trial'
        ? `🎉 4-Day Free Trial Active (${daysLeft} day${daysLeft === 1 ? '' : 's'} remaining)`
        : `👑 ${sub.planType.toUpperCase()} Plan Active (${daysLeft} days remaining)`;
    } else {
      if (deviceClaimedByOther) {
        subMessage = 'Free trial was already claimed on this device by another account. Please subscribe to activate RouteXia.';
      } else if (sub.planType === 'trial' || sub.planType === 'expired') {
        subMessage = 'Your free trial has ended. Please upgrade to Pro.';
      } else {
        subMessage = 'Subscription expired. Please renew your plan.';
      }
    }

    const relays = await this.relayRepository.find({
      where: { isActive: true },
      order: { priority: 'ASC' },
    });

    const relayDtos = relays.map((r) => {
      const loadPct = r.maxCapacity > 0 ? Math.round((r.currentLoad / r.maxCapacity) * 100) : 0;
      return {
        id: r.id,
        region_code: r.regionCode,
        display_name: r.displayName,
        host: r.host,
        port: r.port,
        priority: r.priority,
        is_active: r.isActive,
        max_capacity: r.maxCapacity,
        current_load: r.currentLoad,
        latency_ms: r.latencyMs,
        city: r.city,
        country_code: r.countryCode,
        is_recommended: r.isRecommended,
        load_percent: loadPct,
        high_load_alert: loadPct > 80,
      };
    });

    return {
      token,
      user: {
        id: user.id,
        email: user.email,
        role: user.role,
        is_banned: user.isBanned,
        ban_reason: user.banReason,
        custom_discount_pct: user.customDiscountPct,
        can_manual_select_relay: user.canManualSelectRelay || false,
        referral_code: user.referralCode,
        created_at: user.createdAt,
      },
      subscription: {
        id: sub.id,
        plan_type: sub.planType,
        status: canConnect ? 'active' : 'expired',
        days_left: daysLeft,
        expires_at: sub.expiresAt,
        is_trial: sub.planType === 'trial',
        can_connect: canConnect,
        message: subMessage,
      },
      relays: relayDtos,
    };
  }

  // ── Forgot Password (Custom Branded Link) ─────────────────────────────────
  async forgotPassword(email: string) {
    if (!email) {
      throw new BadRequestException('Email address is required.');
    }

    const resetLink = await this.firebaseAdmin.generatePasswordResetLink(email.trim().toLowerCase());
    return {
      success: true,
      message: `Password reset link generated for ${email}`,
      resetLink,
    };
  }
}
