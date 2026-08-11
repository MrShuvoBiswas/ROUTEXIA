import { Injectable, BadRequestException, UnauthorizedException } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import * as bcrypt from 'bcrypt';
import { UserEntity } from '../../entities/user.entity';
import { DeviceEntity } from '../../entities/device.entity';
import { SubscriptionEntity } from '../../entities/subscription.entity';
import { RelayEntity } from '../../entities/relay.entity';
import { RegisterDto, LoginDto } from './dto/auth.dto';

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
    private jwtService: JwtService,
  ) {}

  async register(dto: RegisterDto) {
    const existing = await this.userRepository.findOne({ where: { email: dto.email.toLowerCase() } });
    if (existing) {
      throw new BadRequestException('Email is already registered');
    }

    const salt = await bcrypt.genSalt(10);
    const passwordHash = await bcrypt.hash(dto.password, salt);

    const user = this.userRepository.create({
      email: dto.email.toLowerCase(),
      passwordHash,
      role: 'user',
      referralCode: 'RX-' + Math.random().toString(36).substring(2, 8).toUpperCase(),
    });
    await this.userRepository.save(user);

    // HWID Anti-Abuse Check
    let device = await this.deviceRepository.findOne({ where: { hwidHash: dto.hwid } });
    let trialDays = 3;

    if (device) {
      // Device has already been registered before
      trialDays = 0; // No free trial for duplicate HWID
    } else {
      device = this.deviceRepository.create({
        hwidHash: dto.hwid,
        firstUserId: user.id,
        trialClaimed: true,
      });
      await this.deviceRepository.save(device);
    }

    // Create initial trial subscription
    const now = new Date();
    const expiresAt = new Date(now.getTime() + trialDays * 24 * 60 * 60 * 1000);

    const subscription = this.subRepository.create({
      userId: user.id,
      hwidHash: dto.hwid,
      planType: trialDays > 0 ? 'trial' : 'expired',
      status: trialDays > 0 ? 'active' : 'expired',
      startsAt: now,
      expiresAt: expiresAt,
    });
    await this.subRepository.save(subscription);

    return this.buildAuthResponse(user, subscription);
  }

  async login(dto: LoginDto) {
    const user = await this.userRepository.findOne({ where: { email: dto.email.toLowerCase() } });
    if (!user) {
      throw new UnauthorizedException('Invalid email or password');
    }

    const validPassword = await bcrypt.compare(dto.password, user.passwordHash);
    if (!validPassword) {
      throw new UnauthorizedException('Invalid email or password');
    }

    if (user.isBanned) {
      throw new UnauthorizedException(`Account banned: ${user.banReason || 'Violation of terms'}`);
    }

    // Fetch active subscription
    let sub = await this.subRepository.findOne({
      where: { userId: user.id },
      order: { expiresAt: 'DESC' },
    });

    if (!sub) {
      const now = new Date();
      sub = this.subRepository.create({
        userId: user.id,
        hwidHash: dto.hwid,
        planType: 'expired',
        status: 'expired',
        startsAt: now,
        expiresAt: now,
      });
      await this.subRepository.save(sub);
    }

    return this.buildAuthResponse(user, sub);
  }

  private async buildAuthResponse(user: UserEntity, sub: SubscriptionEntity) {
    const payload = { sub: user.id, email: user.email, role: user.role };
    const token = this.jwtService.sign(payload);

    const now = new Date();
    const daysLeft = Math.max(0, Math.ceil((new Date(sub.expiresAt).getTime() - now.getTime()) / (1000 * 60 * 60 * 24)));
    const canConnect = !user.isBanned && sub.status === 'active' && sub.expiresAt > now;

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
        message: canConnect ? 'Subscription active' : 'Subscription expired or inactive',
      },
      relays: relayDtos,
    };
  }
}
