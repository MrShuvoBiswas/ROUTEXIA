import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { UserEntity } from '../../entities/user.entity';
import { SubscriptionEntity } from '../../entities/subscription.entity';
import { RelayEntity } from '../../entities/relay.entity';
import { AppVersionEntity } from '../../entities/app-version.entity';

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
  ) {}

  async getAdminStats() {
    const totalUsers = await this.userRepository.count();
    const bannedUsers = await this.userRepository.count({ where: { isBanned: true } });

    const now = new Date();
    const activeSubs = await this.subRepository
      .createQueryBuilder('sub')
      .where('sub.status = :status AND sub.expiresAt > :now', { status: 'active', now })
      .getMany();

    const activeUsersCount = activeSubs.length;
    const trialUsersCount = activeSubs.filter((s) => s.planType === 'trial').length;

    const relays = await this.relayRepository.find({ order: { priority: 'ASC' } });
    const totalRelays = relays.length;
    const activeRelays = relays.filter((r) => r.isActive).length;

    let highLoadAlerts = 0;
    const relayDtos = relays.map((r) => {
      const loadPct = r.maxCapacity > 0 ? Math.round((r.currentLoad / r.maxCapacity) * 100) : 0;
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
    const latestVer = latestVers.length > 0 ? latestVers[0] : null;

    return {
      total_users: totalUsers,
      active_users: activeUsersCount,
      trial_users: trialUsersCount,
      banned_users: bannedUsers,
      total_relays: totalRelays,
      active_relays: activeRelays,
      high_load_alerts: highLoadAlerts,
      latest_version: latestVer ? latestVer.version : '1.0.0',
      relays: relayDtos,
    };
  }
}
