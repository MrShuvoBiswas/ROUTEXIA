import { Controller, Get, Post, Delete, Body, Param, Req } from '@nestjs/common';
import { ApiTags, ApiOperation } from '@nestjs/swagger';
import { SessionsService } from './sessions.service';
import { SessionConnectDto, SessionHeartbeatDto, SessionDisconnectDto } from './dto/session.dto';
import { Request } from 'express';

@ApiTags('Sessions')
@Controller('api/v1/sessions')
export class SessionsController {
  constructor(private readonly sessionsService: SessionsService) {}

  // ── Desktop Client Endpoints ───────────────────────────────────────────

  @Post('connect')
  @ApiOperation({ summary: 'Desktop: Report relay connection and active game' })
  connect(@Body() dto: SessionConnectDto, @Req() req: Request) {
    const clientIp =
      (req.headers['x-forwarded-for'] as string)?.split(',')[0]?.trim() ||
      req.socket?.remoteAddress ||
      'unknown';
    return this.sessionsService.connectSession(dto, clientIp);
  }

  @Post('heartbeat')
  @ApiOperation({ summary: 'Desktop: Send periodic heartbeat with updated stats (every 30s)' })
  heartbeat(@Body() dto: SessionHeartbeatDto) {
    return this.sessionsService.heartbeat(dto);
  }

  @Post('disconnect')
  @ApiOperation({ summary: 'Desktop: Report clean disconnect from relay' })
  disconnect(@Body() dto: SessionDisconnectDto) {
    return this.sessionsService.disconnectSession(dto);
  }

  // ── Admin Endpoints ───────────────────────────────────────────────────

  @Get('admin/live')
  @ApiOperation({ summary: 'Admin: Get all currently live/active user sessions' })
  getLiveSessions() {
    return this.sessionsService.getLiveSessions();
  }

  @Get('admin/all')
  @ApiOperation({ summary: 'Admin: Get all sessions (active + recent history)' })
  getAllSessions() {
    return this.sessionsService.getActiveSessions();
  }

  @Get('admin/user/:userId')
  @ApiOperation({ summary: 'Admin: Get session history for a specific user' })
  getUserHistory(@Param('userId') userId: string) {
    return this.sessionsService.getUserSessionHistory(userId);
  }

  @Delete('admin/:sessionId')
  @ApiOperation({ summary: 'Admin: Force-terminate an active session' })
  terminate(@Param('sessionId') sessionId: string) {
    return this.sessionsService.terminateSession(sessionId);
  }
}
