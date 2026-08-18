import {
  Injectable,
  NotFoundException,
  UnauthorizedException,
} from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { UserEntity } from '../../entities/user.entity';
import { SubscriptionEntity } from '../../entities/subscription.entity';
import { RelayEntity } from '../../entities/relay.entity';
import { AppVersionEntity } from '../../entities/app-version.entity';
import { DeviceEntity } from '../../entities/device.entity';
import { CouponEntity } from '../../entities/coupon.entity';
import { UserSessionEntity } from '../../entities/user-session.entity';
import { UserHistoryEntity } from '../../entities/user-history.entity';

@Injectable()
export class AdminService {
  constructor(
    @InjectRepository(UserEntity)
    private userRepository: Repository<UserEntity>,
    @InjectRepository(SubscriptionEntity)
    private subRepository: Repository<SubscriptionEntity>,
    @InjectRepository(RelayEntity)
    private relayRepository: Repository<RelayEntity>,
    @InjectRepository(AppVersionEntity)
    private versionRepository: Repository<AppVersionEntity>,
    @InjectRepository(DeviceEntity)
    private deviceRepository: Repository<DeviceEntity>,
    @InjectRepository(CouponEntity)
    private couponRepository: Repository<CouponEntity>,
    @InjectRepository(UserSessionEntity)
    private sessionRepository: Repository<UserSessionEntity>,
    @InjectRepository(UserHistoryEntity)
    private historyRepository: Repository<UserHistoryEntity>,
  ) {}

  private static appSettings = {
    allow_manual_relay_selection: false, // Default: Coming Soon globally
    monthly_price: 299,
    quarterly_price: 799,
    yearly_price: 2499,
  };

  static getGlobalAllowManualRelaySelection(): boolean {
    return AdminService.appSettings.allow_manual_relay_selection;
  }

  getAppSettings() {
    return AdminService.appSettings;
  }

  updateAppSettings(payload: Record<string, any>) {
    if (payload.allow_manual_relay_selection !== undefined) {
      AdminService.appSettings.allow_manual_relay_selection = Boolean(payload.allow_manual_relay_selection);
    }
    if (payload.monthly_price !== undefined) AdminService.appSettings.monthly_price = Number(payload.monthly_price);
    if (payload.quarterly_price !== undefined) AdminService.appSettings.quarterly_price = Number(payload.quarterly_price);
    if (payload.yearly_price !== undefined) AdminService.appSettings.yearly_price = Number(payload.yearly_price);

    return { success: true, message: 'App Settings saved successfully', settings: AdminService.appSettings };
  }

  async setUserManualRelayAccess(userId: string, canAccess: boolean, remark?: string, actor = 'Admin') {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);

    user.canManualSelectRelay = canAccess;
    await this.userRepository.save(user);

    await this.logHistory(
      user.id,
      canAccess ? 'MANUAL_RELAY_ACCESS_GRANTED' : 'MANUAL_RELAY_ACCESS_REVOKED',
      canAccess ? 'VIP Manual Server Selection Granted' : 'Manual Server Selection Revoked',
      canAccess ? 'User can manually pick relay nodes' : 'Reverted to global default setting',
      0,
      actor,
      remark || (canAccess ? 'Granted VIP manual server selection' : 'Revoked VIP access'),
    );

    return {
      success: true,
      message: `Manual server selection for ${user.email} ${canAccess ? 'granted (VIP)' : 'revoked (Global Default)'}`,
      can_manual_select_relay: user.canManualSelectRelay,
    };
  }

  // ── Auth ────────────────────────────────────────────────────────────────

  async adminLogin(email: string, password: string) {
    const adminEmail = (process.env.ADMIN_EMAIL || '').toLowerCase();
    const adminPass  = process.env.ADMIN_PASSWORD || '';

    if (!adminEmail || !adminPass) {
      throw new UnauthorizedException('Admin credentials not configured in environment');
    }

    if (email.toLowerCase() !== adminEmail || password !== adminPass) {
      throw new UnauthorizedException('Invalid admin credentials');
    }
    // Simple session token (stateless identifier for single-admin use)
    const token = Buffer.from(`${email}:${Date.now()}`).toString('base64');
    return { access_token: token, email, role: 'super_admin' };
  }

  async changeAdminPassword(currentPassword: string, newPassword: string) {
    const adminPass = process.env.ADMIN_PASSWORD || '';
    if (!adminPass || currentPassword !== adminPass) {
      throw new UnauthorizedException('Current password incorrect');
    }
    return { success: true, message: 'Password change recorded. Update ADMIN_PASSWORD env var to persist.' };
  }

  // ── Dashboard Stats ──────────────────────────────────────────────────────

  async getAdminStats() {
    const totalUsers  = await this.userRepository.count({ where: { isDeleted: false } });
    const bannedUsers = await this.userRepository.count({ where: { isBanned: true, isDeleted: false } });
    const deletedUsers = await this.userRepository.count({ where: { isDeleted: true } });

    const now = new Date();
    const activeSubs = await this.subRepository
      .createQueryBuilder('sub')
      .where('sub.status = :status AND sub.expiresAt > :now', { status: 'active', now })
      .getMany();

    const activeUsersCount = activeSubs.length;
    const trialUsersCount  = activeSubs.filter((s) => s.planType === 'trial').length;

    const relays       = await this.relayRepository.find({ order: { priority: 'ASC' } });
    const totalRelays  = relays.length;
    const activeRelays = relays.filter((r) => r.isActive).length;

    let highLoadAlerts = 0;
    const relayDtos = relays.map((r) => {
      const loadPct = r.maxCapacity > 0
        ? Math.round((r.currentLoad / r.maxCapacity) * 100)
        : 0;
      if (loadPct > 80) highLoadAlerts++;
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

    const latestVers = await this.versionRepository.find({ order: { createdAt: 'DESC' }, take: 1 });
    const latestVer  = latestVers.length > 0 ? latestVers[0] : null;

    return {
      total_users:     totalUsers,
      active_users:    activeUsersCount,
      trial_users:     trialUsersCount,
      banned_users:    bannedUsers,
      deleted_users:   deletedUsers,
      total_relays:    totalRelays,
      active_relays:   activeRelays,
      high_load_alerts: highLoadAlerts,
      latest_version:  latestVer ? latestVer.version : '1.0.0',
      relays:          relayDtos,
    };
  }

  // ── Users ────────────────────────────────────────────────────────────────

  async getUsers(q?: string, plan?: string, status?: string) {
    const users = await this.userRepository.find({
      relations: ['subscriptions'],
      where: { isDeleted: false },
      order: { createdAt: 'DESC' },
    });

    const now = new Date();
    return users
      .filter((u) => !q || u.email.toLowerCase().includes(q.toLowerCase()))
      .map((u) => {
        const activeSub = u.subscriptions
          ?.filter((s) => s.status === 'active' && new Date(s.expiresAt) > now)
          .sort((a, b) => new Date(b.expiresAt).getTime() - new Date(a.expiresAt).getTime())[0];

        const daysLeft = activeSub
          ? Math.max(0, Math.round((new Date(activeSub.expiresAt).getTime() - now.getTime()) / 86400000))
          : 0;

        const planType = activeSub?.planType ?? 'none';

        return {
          id: u.id,
          email: u.email,
          role: u.role,
          is_banned: u.isBanned,
          ban_reason: u.banReason,
          custom_discount_pct: u.customDiscountPct,
          can_manual_select_relay: u.canManualSelectRelay || false,
          referral_code: u.referralCode,
          referred_by: u.referredBy,
          created_at: u.createdAt,
          updated_at: u.updatedAt,
          plan_type: planType,
          days_left: daysLeft,
          expires_at: activeSub?.expiresAt ?? null,
        };
      })
      .filter((u) => {
        if (!plan && !status) return true;
        const planMatch   = !plan   || u.plan_type === plan;
        const statusMatch = !status ||
          (status === 'banned'  && u.is_banned) ||
          (status === 'active'  && !u.is_banned && u.days_left > 0) ||
          (status === 'expired' && !u.is_banned && u.days_left <= 0);
        return planMatch && statusMatch;
      });
  }

  async getDeletedUsers(q?: string) {
    const users = await this.userRepository.find({
      relations: ['subscriptions'],
      where: { isDeleted: true },
      order: { deletedAt: 'DESC', createdAt: 'DESC' },
    });

    const now = new Date();
    return users
      .filter((u) => !q || u.email.toLowerCase().includes(q.toLowerCase()))
      .map((u) => {
        const activeSub = u.subscriptions
          ?.filter((s) => s.status === 'active' && new Date(s.expiresAt) > now)
          .sort((a, b) => new Date(b.expiresAt).getTime() - new Date(a.expiresAt).getTime())[0];

        return {
          id: u.id,
          email: u.email,
          role: u.role,
          is_banned: u.isBanned,
          ban_reason: u.banReason,
          is_deleted: u.isDeleted,
          deleted_at: u.deletedAt,
          custom_discount_pct: u.customDiscountPct,
          referral_code: u.referralCode,
          created_at: u.createdAt,
          updated_at: u.updatedAt,
          plan_type: activeSub?.planType ?? 'none',
          expires_at: activeSub?.expiresAt ?? null,
        };
      });
  }

  private async logHistory(
    userId: string,
    actionType: string,
    title: string,
    details: string,
    daysDelta = 0,
    actor = 'Admin',
    remark = '',
  ) {
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
    } catch (e) {
      console.error('Failed to log user history:', e);
    }
  }

  async getUserHistory(userId: string) {
    if (!userId) throw new NotFoundException('User ID is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }],
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);

    const logs = await this.historyRepository.find({
      where: { userId: user.id },
      order: { createdAt: 'DESC' },
    });

    return logs.map((l) => ({
      id: l.id,
      user_id: l.userId,
      action_type: l.actionType,
      title: l.title,
      details: l.details,
      days_delta: l.daysDelta,
      actor: l.actor,
      remark: l.remark,
      created_at: l.createdAt,
    }));
  }

  async softDeleteUser(userId: string, reason?: string, actor = 'Admin') {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);

    user.isDeleted = true;
    user.deletedAt = new Date();
    user.isBanned  = true;
    user.banReason = reason || 'Account moved to Deleted Accounts by Administrator';
    await this.userRepository.save(user);

    // Terminate all live sessions immediately
    await this.sessionRepository.update(
      { userId: user.id, isActive: true },
      { isActive: false, disconnectedAt: new Date() }
    );

    await this.logHistory(
      user.id,
      'ACCOUNT_DELETED',
      'Moved Account to Trash',
      `Reason: ${reason || 'Admin deleted'}`,
      0,
      actor,
      reason || 'Deleted by admin',
    );

    return { success: true, message: `User ${user.email} moved to Deleted Accounts` };
  }

  async restoreUser(userId: string, actor = 'Admin') {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);

    user.isDeleted = false;
    user.deletedAt = null;
    user.isBanned  = false;
    user.banReason = '';
    await this.userRepository.save(user);

    await this.logHistory(
      user.id,
      'ACCOUNT_RESTORED',
      'Restored Account from Trash',
      'Access restored, unbanned',
      0,
      actor,
      'Restored by admin',
    );

    return { success: true, message: `User ${user.email} restored successfully` };
  }

  async permanentlyDeleteUser(userId: string) {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);

    await this.historyRepository.delete({ userId: user.id });
    await this.sessionRepository.delete({ userId: user.id });
    await this.subRepository.delete({ userId: user.id });
    await this.userRepository.remove(user);

    return { success: true, message: `User ${user.email} permanently deleted from database` };
  }

  async banUser(userId: string, isBanned: boolean, reason?: string, remark?: string, actor = 'Admin') {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);
    user.isBanned  = isBanned;
    user.banReason = isBanned ? (reason || 'Banned by Administrator') : '';
    await this.userRepository.save(user);

    if (isBanned) {
      // Invalidate active sessions immediately
      await this.sessionRepository.update(
        { userId: user.id, isActive: true },
        { isActive: false, disconnectedAt: new Date() }
      );
    }

    await this.logHistory(
      user.id,
      isBanned ? 'ACCOUNT_BANNED' : 'ACCOUNT_UNBANNED',
      isBanned ? 'Account Suspended (Banned)' : 'Account Reactivated (Unbanned)',
      `Reason: ${user.banReason || 'None specified'}`,
      0,
      actor,
      remark || reason || '',
    );

    return { success: true, message: `User ${user.email} ${isBanned ? 'banned' : 'unbanned'}` };
  }

  async extendTrial(userId: string, days: number, remark?: string, actor = 'Admin') {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);

    const now = new Date();
    let sub = await this.subRepository.findOne({
      where: { userId: user.id },
      order: { expiresAt: 'DESC' },
    });

    const baseDate = sub && new Date(sub.expiresAt) > now ? new Date(sub.expiresAt) : now;
    const newExpiresAt = new Date(baseDate.getTime() + Math.abs(days) * 86400000);

    if (sub) {
      sub.status    = 'active';
      sub.expiresAt = newExpiresAt;
    } else {
      sub = this.subRepository.create({
        userId: user.id,
        hwidHash: 'ADMIN-TRIAL-GRANT',
        planType: 'trial',
        status: 'active',
        startsAt: now,
        expiresAt: newExpiresAt,
      });
    }
    await this.subRepository.save(sub);

    await this.logHistory(
      user.id,
      'TRIAL_EXTENDED',
      `Extended Access (+${days} Days)`,
      `Plan: ${sub.planType} • New Expiry: ${newExpiresAt.toLocaleDateString()}`,
      days,
      actor,
      remark || 'Trial extended by Admin',
    );

    return { success: true, message: `Extended ${user.email} trial by ${days} days` };
  }

  async reduceDays(userId: string, days: number, remark?: string, actor = 'Admin') {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);

    const now = new Date();
    const sub = await this.subRepository.findOne({
      where: { userId: user.id },
      order: { expiresAt: 'DESC' },
    });

    if (!sub || new Date(sub.expiresAt) <= now) {
      throw new NotFoundException(`User ${user.email} has no active subscription or trial to reduce`);
    }

    const currentExpiry = new Date(sub.expiresAt);
    const newExpiresAt = new Date(currentExpiry.getTime() - Math.abs(days) * 86400000);

    let isTerminated = false;
    if (newExpiresAt <= now) {
      sub.status = 'expired';
      sub.expiresAt = now;
      isTerminated = true;

      // Invalidate active sessions
      await this.sessionRepository.update(
        { userId: user.id, isActive: true },
        { isActive: false, disconnectedAt: new Date() }
      );
    } else {
      sub.expiresAt = newExpiresAt;
    }
    await this.subRepository.save(sub);

    await this.logHistory(
      user.id,
      isTerminated ? 'TRIAL_TERMINATED' : 'TRIAL_REDUCED',
      `Reduced Access (-${days} Days)`,
      isTerminated ? 'Deduction resulted in plan expiry' : `New Expiry: ${newExpiresAt.toLocaleDateString()}`,
      -days,
      actor,
      remark || 'Days deducted by Admin',
    );

    return {
      success: true,
      message: isTerminated
        ? `Reduced ${days} days. Plan for ${user.email} has now expired.`
        : `Deducted ${days} days from ${user.email}. New expiry: ${newExpiresAt.toLocaleDateString()}`,
    };
  }

  async terminatePlan(userId: string, remark?: string, actor = 'Admin') {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);

    const now = new Date();
    const sub = await this.subRepository.findOne({
      where: { userId: user.id },
      order: { expiresAt: 'DESC' },
    });

    if (sub) {
      sub.status = 'expired';
      sub.expiresAt = now;
      await this.subRepository.save(sub);
    }

    // Invalidate active sessions immediately
    await this.sessionRepository.update(
      { userId: user.id, isActive: true },
      { isActive: false, disconnectedAt: new Date() }
    );

    await this.logHistory(
      user.id,
      'TRIAL_TERMINATED',
      'Terminated Active Subscription / Trial',
      'Plan revoked immediately (0 days remaining)',
      0,
      actor,
      remark || 'Plan terminated by Admin',
    );

    return { success: true, message: `Active plan / trial for ${user.email} terminated immediately` };
  }

  async setDiscount(userId: string, discountPct: number, remark?: string, actor = 'Admin') {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);
    const prev = user.customDiscountPct;
    user.customDiscountPct = Math.max(0, Math.min(100, discountPct));
    await this.userRepository.save(user);

    await this.logHistory(
      user.id,
      'DISCOUNT_SET',
      `Custom Discount Set (${user.customDiscountPct}%)`,
      `Previous: ${prev}% → New: ${user.customDiscountPct}%`,
      0,
      actor,
      remark || 'Custom discount updated',
    );

    return { success: true, message: `Discount for ${user.email} set to ${discountPct}%` };
  }

  async grantSubscription(userId: string, planType: string, customDays?: number, remark?: string, actor = 'Admin') {
    if (!userId) throw new NotFoundException('User ID or Email is required');
    const user = await this.userRepository.findOne({
      where: [{ id: userId }, { email: userId }]
    });
    if (!user) throw new NotFoundException(`User ${userId} not found`);

    const PLAN_DAYS: Record<string, number> = { trial: 7, monthly: 30, quarterly: 90, yearly: 365 };
    const days = customDays && customDays > 0 ? customDays : (PLAN_DAYS[planType] ?? 30);
    const now  = new Date();
    const expiresAt = new Date(now.getTime() + days * 86400000);

    let sub = await this.subRepository.findOne({
      where: { userId: user.id },
      order: { expiresAt: 'DESC' },
    });

    if (sub) {
      const base = new Date(sub.expiresAt) > now ? new Date(sub.expiresAt) : now;
      sub.planType  = planType;
      sub.status    = 'active';
      sub.expiresAt = new Date(base.getTime() + days * 86400000);
    } else {
      sub = this.subRepository.create({
        userId: user.id,
        hwidHash: 'ADMIN-FREE-GRANT',
        planType,
        status: 'active',
        startsAt: now,
        expiresAt,
      });
    }
    await this.subRepository.save(sub);

    await this.logHistory(
      user.id,
      'SUB_GRANTED',
      `Granted ${planType.toUpperCase()} Plan (+${days} Days)`,
      `Expires: ${sub.expiresAt.toLocaleDateString()}`,
      days,
      actor,
      remark || `Granted ${planType} plan free`,
    );

    return { success: true, message: `Granted ${planType} plan (${days} days) to ${user.email}` };
  }

  // ── Subscriptions ────────────────────────────────────────────────────────

  async getAllSubscriptions() {
    const subs = await this.subRepository.find({ order: { createdAt: 'DESC' } });
    return subs.map((s) => ({
      id: s.id,
      user_id: s.userId,
      hwid_hash: s.hwidHash,
      plan_type: s.planType,
      status: s.status,
      starts_at: s.startsAt,
      expires_at: s.expiresAt,
      created_at: s.createdAt,
    }));
  }

  async extendSubscriptionById(subId: string, days: number) {
    const sub = await this.subRepository.findOne({ where: { id: subId } });
    if (!sub) throw new NotFoundException(`Subscription ${subId} not found`);
    const now  = new Date();
    const base = new Date(sub.expiresAt) > now ? new Date(sub.expiresAt) : now;
    sub.expiresAt = new Date(base.getTime() + days * 86400000);
    sub.status    = 'active';
    await this.subRepository.save(sub);
    return { success: true, message: `Subscription extended by ${days} days` };
  }

  async revokeSubscription(subId: string) {
    const sub = await this.subRepository.findOne({ where: { id: subId } });
    if (!sub) throw new NotFoundException(`Subscription ${subId} not found`);
    sub.status    = 'expired';
    sub.expiresAt = new Date();
    await this.subRepository.save(sub);
    return { success: true, message: 'Subscription revoked' };
  }

  // ── Devices ──────────────────────────────────────────────────────────────

  async getAllDevices() {
    const devices = await this.deviceRepository.find({ order: { firstClaimedAt: 'DESC' } });
    return devices.map((d) => ({
      hwid_hash:       d.hwidHash,
      first_user_id:   d.firstUserId,
      trial_claimed:   d.trialClaimed,
      is_banned:       d.isBanned,
      ban_reason:      d.banReason,
      first_claimed_at: d.firstClaimedAt,
    }));
  }

  async banDevice(hwidHash: string, reason?: string) {
    const device = await this.deviceRepository.findOne({ where: { hwidHash } });
    if (!device) throw new NotFoundException(`Device ${hwidHash} not found`);
    device.isBanned  = true;
    device.banReason = reason || 'Banned by Administrator';
    await this.deviceRepository.save(device);
    return { success: true, message: `Device ${hwidHash.substring(0, 12)}… banned` };
  }

  async unbanDevice(hwidHash: string) {
    const device = await this.deviceRepository.findOne({ where: { hwidHash } });
    if (!device) throw new NotFoundException(`Device ${hwidHash} not found`);
    device.isBanned  = false;
    device.banReason = '';
    await this.deviceRepository.save(device);
    return { success: true, message: `Device ${hwidHash.substring(0, 12)}… unbanned` };
  }

  // ── Relays ───────────────────────────────────────────────────────────────

  async getAllRelays() {
    const relays = await this.relayRepository.find({ order: { priority: 'ASC', regionCode: 'ASC' } });
    return relays.map((r) => ({
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
    }));
  }

  async createRelay(body: any) {
    const relay = this.relayRepository.create({
      regionCode:    body.region_code,
      displayName:   body.display_name,
      host:          body.host,
      port:          body.port || 9001,
      maxCapacity:   body.max_capacity || 500,
      priority:      body.priority || 1,
      city:          body.city || '',
      countryCode:   body.country_code || '',
      isRecommended: body.is_recommended ?? true,
      isActive:      true,
      currentLoad:   0,
      latencyMs:     40,
    });
    await this.relayRepository.save(relay);
    return { success: true, relay };
  }

  async updateRelay(id: string, body: any) {
    const relay = await this.relayRepository.findOne({ where: { id } });
    if (!relay) throw new NotFoundException(`Relay ${id} not found`);
    if (body.display_name  !== undefined) relay.displayName   = body.display_name;
    if (body.host          !== undefined) relay.host          = body.host;
    if (body.port          !== undefined) relay.port          = body.port;
    if (body.max_capacity  !== undefined) relay.maxCapacity   = body.max_capacity;
    if (body.priority      !== undefined) relay.priority      = body.priority;
    if (body.city          !== undefined) relay.city          = body.city;
    if (body.is_recommended!== undefined) relay.isRecommended = body.is_recommended;
    await this.relayRepository.save(relay);
    return { success: true, relay };
  }

  async toggleRelay(id: string, isActive: boolean) {
    const relay = await this.relayRepository.findOne({ where: { id } });
    if (!relay) throw new NotFoundException(`Relay ${id} not found`);
    relay.isActive = isActive;
    await this.relayRepository.save(relay);
    return { success: true, message: `Relay ${isActive ? 'enabled' : 'disabled'}` };
  }

  async deleteRelay(id: string) {
    const relay = await this.relayRepository.findOne({ where: { id } });
    if (!relay) throw new NotFoundException(`Relay ${id} not found`);
    await this.relayRepository.remove(relay);
    return { success: true, message: 'Relay deleted' };
  }

  // ── Coupons ──────────────────────────────────────────────────────────────

  async getCoupons() {
    const coupons = await this.couponRepository.find({ order: { createdAt: 'DESC' } });
    return coupons.map((c) => ({
      id:           c.id,
      code:         c.code,
      discount_pct: c.discountPct,
      max_uses:     c.maxUses,
      used_count:   c.usedCount,
      expires_at:   c.expiresAt,
      created_at:   c.createdAt,
    }));
  }

  async createCoupon(body: { code: string; discount_pct: number; max_uses: number; expires_at?: string }) {
    const coupon = this.couponRepository.create({
      code:        body.code.toUpperCase(),
      discountPct: body.discount_pct,
      maxUses:     body.max_uses || 100,
      usedCount:   0,
      expiresAt:   body.expires_at ? new Date(body.expires_at) : null,
    });
    await this.couponRepository.save(coupon);
    return { success: true, coupon };
  }

  async deactivateCoupon(id: string) {
    const coupon = await this.couponRepository.findOne({ where: { id } });
    if (!coupon) throw new NotFoundException(`Coupon ${id} not found`);
    // Mark as exhausted by setting usedCount = maxUses
    coupon.usedCount = coupon.maxUses;
    await this.couponRepository.save(coupon);
    return { success: true, message: `Coupon ${coupon.code} deactivated` };
  }

  // ── OTA Releases ─────────────────────────────────────────────────────────

  async getAllReleases() {
    const releases = await this.versionRepository.find({ order: { createdAt: 'DESC' } });
    return releases.map((r) => ({
      id:                    r.id,
      version:               r.version,
      release_notes:         r.releaseNotes,
      download_url:          r.downloadUrl,
      checksum_sha256:       r.checksumSha256,
      is_mandatory:          r.isMandatory,
      min_supported_version: r.minSupportedVersion,
      silent_update:         r.silentUpdate,
      is_active:             r.isActive,
      file_size_bytes:       r.fileSizeBytes,
      created_at:            r.createdAt,
    }));
  }

  async publishRelease(body: any) {
    const release = this.versionRepository.create({
      version:              body.version,
      releaseNotes:         body.release_notes || '',
      downloadUrl:          body.download_url,
      checksumSha256:       body.checksum_sha256 || '',
      isMandatory:          body.is_mandatory ?? false,
      minSupportedVersion:  body.min_supported_version || '1.0.0',
      silentUpdate:         body.silent_update ?? true,
      isActive:             body.is_active ?? true,
      fileSizeBytes:        body.file_size_bytes || 0,
    });
    await this.versionRepository.save(release);
    return { success: true, release };
  }

  async updateRelease(id: string, body: any) {
    const release = await this.versionRepository.findOne({ where: { id } });
    if (!release) throw new NotFoundException(`Release ${id} not found`);
    if (body.is_mandatory !== undefined) release.isMandatory   = body.is_mandatory;
    if (body.is_active    !== undefined) release.isActive      = body.is_active;
    if (body.silent_update!== undefined) release.silentUpdate  = body.silent_update;
    if (body.release_notes!== undefined) release.releaseNotes  = body.release_notes;
    await this.versionRepository.save(release);
    return { success: true, release };
  }

  async deleteRelease(id: string) {
    const release = await this.versionRepository.findOne({ where: { id } });
    if (!release) throw new NotFoundException(`Release ${id} not found`);
    await this.versionRepository.remove(release);
    return { success: true, message: 'Release deleted' };
  }
}
