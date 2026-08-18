import { Injectable, NotFoundException, UnauthorizedException, ForbiddenException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { UserSessionEntity } from '../../entities/user-session.entity';
import { UserEntity } from '../../entities/user.entity';
import { SubscriptionEntity } from '../../entities/subscription.entity';
import { DeviceEntity } from '../../entities/device.entity';
import { SessionConnectDto, SessionHeartbeatDto, SessionDisconnectDto } from './dto/session.dto';

@Injectable()
export class SessionsService {
  constructor(
    @InjectRepository(UserSessionEntity)
    private sessionRepo: Repository<UserSessionEntity>,
    @InjectRepository(UserEntity)
    private userRepo: Repository<UserEntity>,
    @InjectRepository(SubscriptionEntity)
    private subRepo: Repository<SubscriptionEntity>,
    @InjectRepository(DeviceEntity)
    private deviceRepo: Repository<DeviceEntity>,
  ) {}

  // Called by desktop client when it connects to a relay
  async connectSession(dto: SessionConnectDto, clientIp: string) {
    const user = await this.userRepo.findOne({ where: { id: dto.userId } });
    if (!user) throw new NotFoundException('User not found');

    if (user.isDeleted) {
      throw new UnauthorizedException('Account has been deleted by an administrator.');
    }

    if (user.isBanned) {
      throw new UnauthorizedException(`Account suspended: ${user.banReason || 'Access denied'}`);
    }

    // Check device ban
    if (dto.hwid) {
      const device = await this.deviceRepo.findOne({ where: { hwidHash: dto.hwid } });
      if (device?.isBanned) {
        throw new UnauthorizedException(`Hardware device banned: ${device.banReason || 'Access denied'}`);
      }
    }

    // Check active subscription
    const sub = await this.subRepo.findOne({
      where: { userId: dto.userId },
      order: { expiresAt: 'DESC' },
    });

    const now = new Date();
    if (!sub || sub.status !== 'active' || new Date(sub.expiresAt) <= now) {
      throw new ForbiddenException('Active subscription required. Please subscribe to use RouteXia relays.');
    }

    // HWID Anti-Abuse on Trial: Check if this hardware already claimed trial under another account
    if (sub.planType === 'trial' && dto.hwid) {
      const device = await this.deviceRepo.findOne({ where: { hwidHash: dto.hwid } });
      if (device && device.trialClaimed && device.firstUserId && device.firstUserId !== user.id) {
        sub.status = 'expired';
        sub.planType = 'expired';
        sub.expiresAt = now;
        await this.subRepo.save(sub);
        throw new ForbiddenException('Free trial was already claimed on this computer by another account. Please subscribe to continue.');
      }
    }

    // Close any existing active sessions for this user
    await this.sessionRepo.update(
      { userId: dto.userId, isActive: true },
      { isActive: false, disconnectedAt: new Date() }
    );

    const session = this.sessionRepo.create({
      userId: dto.userId,
      userEmail: user.email,
      relayId: dto.relayId,
      relayName: dto.relayName,
      relayRegion: dto.relayRegion,
      relayHost: dto.relayHost,
      gameName: dto.gameName || null,
      gameProcess: dto.gameProcess || null,
      pingMs: dto.pingMs || 0,
      downloadMbps: 0,
      uploadMbps: 0,
      clientIp,
      clientVersion: dto.clientVersion || '1.0.0',
      hwid: dto.hwid || null,
      isActive: true,
    });

    await this.sessionRepo.save(session);
    return { session_id: session.id, connected_at: session.connectedAt };
  }

  // Called by desktop client to report live stats & verify real-time authorization
  async heartbeat(dto: SessionHeartbeatDto) {
    const session = await this.sessionRepo.findOne({ where: { id: dto.sessionId, isActive: true } });
    if (!session) throw new NotFoundException('Session not found or inactive');

    // Real-time Ban & Deletion Check
    const user = await this.userRepo.findOne({ where: { id: session.userId } });
    if (!user || user.isDeleted) {
      session.isActive = false;
      session.disconnectedAt = new Date();
      await this.sessionRepo.save(session);
      throw new UnauthorizedException('Account has been deleted by an administrator');
    }
    if (user.isBanned) {
      session.isActive = false;
      session.disconnectedAt = new Date();
      await this.sessionRepo.save(session);
      throw new UnauthorizedException(`Account suspended: ${user.banReason || 'Violated terms of service'}`);
    }

    // Real-time Subscription Check
    const sub = await this.subRepo.findOne({
      where: { userId: user.id },
      order: { expiresAt: 'DESC' },
    });
    if (!sub || sub.status !== 'active' || new Date(sub.expiresAt) <= new Date()) {
      session.isActive = false;
      session.disconnectedAt = new Date();
      await this.sessionRepo.save(session);
      throw new ForbiddenException('Subscription has expired');
    }

    if (dto.pingMs !== undefined) session.pingMs = dto.pingMs;
    if (dto.downloadMbps !== undefined) session.downloadMbps = dto.downloadMbps;
    if (dto.uploadMbps !== undefined) session.uploadMbps = dto.uploadMbps;
    if (dto.bytesSent !== undefined) session.bytesSent = dto.bytesSent;
    if (dto.bytesReceived !== undefined) session.bytesReceived = dto.bytesReceived;
    if (dto.gameName !== undefined) session.gameName = dto.gameName;
    if (dto.gameProcess !== undefined) session.gameProcess = dto.gameProcess;

    session.lastHeartbeat = new Date();

    await this.sessionRepo.save(session);
    return { ok: true };
  }

  // Called by desktop client when disconnecting
  async disconnectSession(dto: SessionDisconnectDto) {
    const session = await this.sessionRepo.findOne({ where: { id: dto.sessionId } });
    if (!session) throw new NotFoundException('Session not found');

    session.isActive = false;
    session.disconnectedAt = new Date();
    if (dto.bytesSent !== undefined) session.bytesSent = dto.bytesSent;
    if (dto.bytesReceived !== undefined) session.bytesReceived = dto.bytesReceived;
    await this.sessionRepo.save(session);
    return { ok: true };
  }

  // Admin: get all active sessions
  async getActiveSessions() {
    // Auto-expire sessions with no heartbeat for > 5 minutes
    const staleThreshold = new Date(Date.now() - 5 * 60 * 1000);
    await this.sessionRepo
      .createQueryBuilder()
      .update(UserSessionEntity)
      .set({ isActive: false, disconnectedAt: new Date() })
      .where('isActive = :active AND lastHeartbeat < :threshold', { active: true, threshold: staleThreshold })
      .execute();

    const activeSessions = await this.sessionRepo.find({
      order: { connectedAt: 'DESC' },
      take: 100,
    });

    return activeSessions.map(s => ({
      id: s.id,
      user_id: s.userId,
      user_email: s.userEmail,
      relay_id: s.relayId,
      relay_name: s.relayName,
      relay_region: s.relayRegion,
      relay_host: s.relayHost,
      game_name: s.gameName,
      game_process: s.gameProcess,
      ping_ms: s.pingMs,
      download_mbps: s.downloadMbps,
      upload_mbps: s.uploadMbps,
      bytes_sent: s.bytesSent || 0,
      bytes_received: s.bytesReceived || 0,
      client_ip: s.clientIp,
      client_version: s.clientVersion,
      hwid: s.hwid,
      is_active: s.isActive,
      connected_at: s.connectedAt,
      last_heartbeat: s.lastHeartbeat,
      disconnected_at: s.disconnectedAt,
      duration_minutes: Math.round((Date.now() - new Date(s.connectedAt).getTime()) / 60000),
    }));
  }

  // Admin: get live active sessions only (with recent heartbeat)
  async getLiveSessions() {
    const staleThreshold = new Date(Date.now() - 5 * 60 * 1000);
    const sessions = await this.sessionRepo
      .createQueryBuilder('s')
      .where('s.isActive = true AND s.lastHeartbeat > :threshold', { threshold: staleThreshold })
      .orderBy('s.connectedAt', 'DESC')
      .getMany();

    return sessions.map(s => ({
      id: s.id,
      user_id: s.userId,
      user_email: s.userEmail,
      relay_id: s.relayId,
      relay_name: s.relayName,
      relay_region: s.relayRegion,
      relay_host: s.relayHost,
      game_name: s.gameName,
      game_process: s.gameProcess,
      ping_ms: s.pingMs,
      download_mbps: s.downloadMbps,
      upload_mbps: s.uploadMbps,
      bytes_sent: s.bytesSent || 0,
      bytes_received: s.bytesReceived || 0,
      client_ip: s.clientIp,
      client_version: s.clientVersion,
      is_active: s.isActive,
      connected_at: s.connectedAt,
      last_heartbeat: s.lastHeartbeat,
      duration_minutes: Math.round((Date.now() - new Date(s.connectedAt).getTime()) / 60000),
    }));
  }

  // Admin: get session history for a user
  async getUserSessionHistory(userId: string) {
    const sessions = await this.sessionRepo.find({
      where: { userId },
      order: { connectedAt: 'DESC' },
      take: 50,
    });
    return sessions;
  }

  // Admin: terminate a session
  async terminateSession(sessionId: string) {
    const session = await this.sessionRepo.findOne({ where: { id: sessionId } });
    if (!session) throw new NotFoundException('Session not found');
    session.isActive = false;
    session.disconnectedAt = new Date();
    await this.sessionRepo.save(session);
    return { ok: true, message: 'Session terminated by admin' };
  }
}
