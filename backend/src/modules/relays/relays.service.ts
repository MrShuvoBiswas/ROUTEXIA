import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { RelayEntity } from '../../entities/relay.entity';
import { UserSessionEntity } from '../../entities/user-session.entity';
import { AddRelayDto, UpdateRelayDto, RelayTelemetryDto } from './dto/relay.dto';

@Injectable()
export class RelaysService {
  constructor(
    @InjectRepository(RelayEntity)
    private relayRepository: Repository<RelayEntity>,
    @InjectRepository(UserSessionEntity)
    private sessionRepository: Repository<UserSessionEntity>,
  ) {}

  async getActiveRelays() {
    const relays = await this.relayRepository.find({
      order: { priority: 'ASC' },
    });

    const staleThreshold = new Date(Date.now() - 5 * 60 * 1000);
    const liveSessions = await this.sessionRepository
      .createQueryBuilder('s')
      .where('s.isActive = true AND s.lastHeartbeat > :threshold', { threshold: staleThreshold })
      .getMany();

    const allSessions = await this.sessionRepository.find({
      select: ['relayId', 'relayHost', 'bytesSent', 'bytesReceived'],
    });

    return relays.map((r) => {
      // Find active sessions for this relay node
      const activeMatching = liveSessions.filter(
        (s) => s.relayId === r.id || s.relayHost === r.host,
      );
      const activeUsers = activeMatching.length;

      // Calculate live throughput
      const downloadMbps = Number(
        activeMatching.reduce((acc, s) => acc + (s.downloadMbps || 0), 0).toFixed(2),
      );
      const uploadMbps = Number(
        activeMatching.reduce((acc, s) => acc + (s.uploadMbps || 0), 0).toFixed(2),
      );
      const liveBandwidth = Number((downloadMbps + uploadMbps).toFixed(2));

      // Calculate total transferred data across all sessions
      const matchingAll = allSessions.filter(
        (s) => s.relayId === r.id || s.relayHost === r.host,
      );
      const totalSentBytes = matchingAll.reduce((acc, s) => acc + Number(s.bytesSent || 0), 0) + Number(r.totalBytesSent || 0);
      const totalRecvBytes = matchingAll.reduce((acc, s) => acc + Number(s.bytesReceived || 0), 0) + Number(r.totalBytesReceived || 0);

      // Check if relay server sent direct telemetry within last 2 minutes
      const hasRecentTelemetry =
        r.lastTelemetryAt &&
        new Date().getTime() - new Date(r.lastTelemetryAt).getTime() < 120000;

      const cpuUsage = hasRecentTelemetry && r.cpuUsage > 0
        ? r.cpuUsage
        : Math.min(98.5, Number((4.2 + activeUsers * 2.5 + liveBandwidth * 0.6).toFixed(1)));

      const ramUsage = hasRecentTelemetry && r.ramUsage > 0
        ? r.ramUsage
        : Math.min(92.0, Number((18.5 + activeUsers * 1.6).toFixed(1)));

      const maxCap = r.maxCapacity > 0 ? r.maxCapacity : 500;
      const loadPct = Math.min(100, Math.round((activeUsers / maxCap) * 100));

      return {
        id: r.id,
        region_code: r.regionCode,
        display_name: r.displayName,
        host: r.host,
        port: r.port,
        priority: r.priority,
        is_active: r.isActive,
        max_capacity: maxCap,
        current_load: activeUsers,
        active_users: activeUsers,
        latency_ms: r.latencyMs,
        city: r.city,
        country_code: r.countryCode,
        is_recommended: r.isRecommended,
        load_percent: loadPct,
        high_load_alert: loadPct >= 80 || cpuUsage >= 80,
        cpu_usage: cpuUsage,
        ram_usage: ramUsage,
        ram_total_gb: r.ramTotalGb || 2.0,
        total_bytes_sent: totalSentBytes,
        total_bytes_received: totalRecvBytes,
        download_mbps: downloadMbps,
        upload_mbps: uploadMbps,
        current_bandwidth_mbps: liveBandwidth,
        last_telemetry_at: r.lastTelemetryAt,
      };
    });
  }

  async reportTelemetry(dto: RelayTelemetryDto) {
    let relay = await this.relayRepository.findOne({ where: { host: dto.host } });
    if (!relay && dto.port) {
      relay = await this.relayRepository.findOne({ where: { host: dto.host, port: dto.port } });
    }
    if (!relay) {
      throw new NotFoundException(`Relay with host ${dto.host} not registered`);
    }

    if (dto.cpuUsage !== undefined) relay.cpuUsage = dto.cpuUsage;
    if (dto.ramUsage !== undefined) relay.ramUsage = dto.ramUsage;
    if (dto.ramTotalGb !== undefined) relay.ramTotalGb = dto.ramTotalGb;
    if (dto.totalBytesSent !== undefined) relay.totalBytesSent = dto.totalBytesSent;
    if (dto.totalBytesReceived !== undefined) relay.totalBytesReceived = dto.totalBytesReceived;
    if (dto.currentBandwidthMbps !== undefined) relay.currentBandwidthMbps = dto.currentBandwidthMbps;
    if (dto.activeSessions !== undefined) relay.currentLoad = dto.activeSessions;
    relay.lastTelemetryAt = new Date();

    await this.relayRepository.save(relay);
    return { ok: true, relay_id: relay.id, updated_at: relay.lastTelemetryAt };
  }

  async addRelay(dto: AddRelayDto) {
    const relay = this.relayRepository.create({
      regionCode: dto.regionCode,
      displayName: dto.displayName,
      host: dto.host,
      port: dto.port || 9001,
      priority: dto.priority || 1,
      maxCapacity: dto.maxCapacity || 500,
      city: dto.city || 'Singapore',
      countryCode: dto.countryCode || 'SG',
      isRecommended: dto.isRecommended !== undefined ? dto.isRecommended : true,
    });
    return this.relayRepository.save(relay);
  }

  async updateRelay(id: string, dto: UpdateRelayDto) {
    const relay = await this.relayRepository.findOne({ where: { id } });
    if (!relay) {
      throw new NotFoundException(`Relay server with ID ${id} not found`);
    }
    Object.assign(relay, dto);
    return this.relayRepository.save(relay);
  }

  async deleteRelay(id: string) {
    const relay = await this.relayRepository.findOne({ where: { id } });
    if (!relay) {
      throw new NotFoundException(`Relay server with ID ${id} not found`);
    }
    await this.relayRepository.remove(relay);
    return { success: true, message: `Relay ${id} removed successfully` };
  }

  async clearAllRelays() {
    await this.relayRepository.clear();
    return { success: true, message: 'All relay servers cleared from inventory' };
  }
}
