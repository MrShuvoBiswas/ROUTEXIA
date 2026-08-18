import {
  Controller, Get, Post, Patch, Delete, Body, Param, Query, UseGuards
} from '@nestjs/common';
import { ApiTags, ApiOperation, ApiBearerAuth } from '@nestjs/swagger';
import { AdminService } from './admin.service';

@ApiTags('Admin')
@Controller('api/v1/admin')
export class AdminController {
  constructor(private readonly adminService: AdminService) {}

  // ── Auth ─────────────────────────────────────────────────────────────────
  @Post('auth/login')
  @ApiOperation({ summary: 'Admin: Authenticate with credentials → JWT session' })
  adminLogin(@Body() body: { email: string; password: string }) {
    return this.adminService.adminLogin(body.email, body.password);
  }

  @Post('auth/change-password')
  @ApiOperation({ summary: 'Admin: Change admin password' })
  changePassword(@Body() body: { current_password?: string; currentPassword?: string; new_password?: string; newPassword?: string }) {
    const currentPass = body.current_password || body.currentPassword || '';
    const newPass = body.new_password || body.newPassword || '';
    return this.adminService.changeAdminPassword(currentPass, newPass);
  }

  // ── Dashboard ─────────────────────────────────────────────────────────────
  @Get('stats')
  @ApiOperation({ summary: 'Admin: Get aggregated system overview statistics' })
  getStats() {
    return this.adminService.getAdminStats();
  }

  // ── Users ─────────────────────────────────────────────────────────────────
  @Get('users')
  @ApiOperation({ summary: 'Admin: List all users with subscription info' })
  getUsers(@Query('q') q?: string, @Query('plan') plan?: string, @Query('status') status?: string) {
    return this.adminService.getUsers(q, plan, status);
  }

  @Get('users/:id/history')
  @ApiOperation({ summary: 'Admin: Get full passbook/history ledger for a user' })
  getUserHistory(@Param('id') id: string) {
    return this.adminService.getUserHistory(id);
  }

  @Post('users/ban')
  @ApiOperation({ summary: 'Admin: Ban or unban a user account with custom remark' })
  banUser(@Body() body: Record<string, any>) {
    const userId = body?.user_id || body?.userId;
    const isBanned = body?.is_banned !== undefined ? Boolean(body.is_banned) : (body?.isBanned !== undefined ? Boolean(body.isBanned) : true);
    const reason = body?.reason;
    const remark = body?.remark || reason;
    const actor = body?.actor || 'Admin';
    return this.adminService.banUser(userId, isBanned, reason, remark, actor);
  }

  @Post('users/trial')
  @ApiOperation({ summary: 'Admin: Extend trial days for a user with remark' })
  extendTrial(@Body() body: Record<string, any>) {
    const userId = body?.user_id || body?.userId;
    const days = Number(body?.days || 7);
    const remark = body?.remark;
    const actor = body?.actor || 'Admin';
    return this.adminService.extendTrial(userId, days, remark, actor);
  }

  @Post('users/reduce-days')
  @ApiOperation({ summary: 'Admin: Reduce trial/subscription days with remark' })
  reduceDays(@Body() body: Record<string, any>) {
    const userId = body?.user_id || body?.userId;
    const days = Number(body?.days || 1);
    const remark = body?.remark;
    const actor = body?.actor || 'Admin';
    return this.adminService.reduceDays(userId, days, remark, actor);
  }

  @Post('users/terminate-plan')
  @ApiOperation({ summary: 'Admin: Immediately terminate active trial or subscription with remark' })
  terminatePlan(@Body() body: Record<string, any>) {
    const userId = body?.user_id || body?.userId;
    const remark = body?.remark;
    const actor = body?.actor || 'Admin';
    return this.adminService.terminatePlan(userId, remark, actor);
  }

  @Post('users/discount')
  @ApiOperation({ summary: 'Admin: Set custom discount percentage for a user with remark' })
  setDiscount(@Body() body: Record<string, any>) {
    const userId = body?.user_id || body?.userId;
    const discountPct = body?.discount_pct !== undefined ? Number(body.discount_pct) : Number(body?.discountPct || 0);
    const remark = body?.remark;
    const actor = body?.actor || 'Admin';
    return this.adminService.setDiscount(userId, discountPct, remark, actor);
  }

  @Post('users/grant-sub')
  @ApiOperation({ summary: 'Admin: Grant a free subscription plan to a user with remark' })
  grantSub(@Body() body: Record<string, any>) {
    const userId = body?.user_id || body?.userId;
    const planType = body?.plan_type || body?.planType || 'monthly';
    const days = body?.days ? Number(body.days) : undefined;
    const remark = body?.remark;
    const actor = body?.actor || 'Admin';
    return this.adminService.grantSubscription(userId, planType, days, remark, actor);
  }

  @Get('users/deleted')
  @ApiOperation({ summary: 'Admin: List soft-deleted user accounts in Trash' })
  getDeletedUsers(@Query('q') q?: string) {
    return this.adminService.getDeletedUsers(q);
  }

  @Post('users/delete')
  @ApiOperation({ summary: 'Admin: Move a user account to Deleted Accounts (Trash)' })
  deleteUser(@Body() body: Record<string, any>) {
    const userId = body?.user_id || body?.userId;
    const reason = body?.reason || body?.remark;
    const actor = body?.actor || 'Admin';
    return this.adminService.softDeleteUser(userId, reason, actor);
  }

  @Post('users/restore')
  @ApiOperation({ summary: 'Admin: Restore an account from Deleted Accounts (Undo Delete)' })
  restoreUser(@Body() body: Record<string, any>) {
    const userId = body?.user_id || body?.userId;
    const actor = body?.actor || 'Admin';
    return this.adminService.restoreUser(userId, actor);
  }

  @Delete('users/permanent/:id')
  @ApiOperation({ summary: 'Admin: Permanently wipe a user account and subscriptions' })
  permanentlyDeleteUser(@Param('id') id: string) {
    return this.adminService.permanentlyDeleteUser(id);
  }

  // ── Subscriptions ─────────────────────────────────────────────────────────
  @Get('subscriptions')
  @ApiOperation({ summary: 'Admin: List all subscription records' })
  getSubscriptions() {
    return this.adminService.getAllSubscriptions();
  }

  @Post('subscriptions/extend')
  @ApiOperation({ summary: 'Admin: Extend an existing subscription expiry' })
  extendSub(@Body() body: Record<string, any>) {
    const subId = body?.sub_id || body?.subId;
    const days = Number(body?.days || 30);
    return this.adminService.extendSubscriptionById(subId, days);
  }

  @Post('subscriptions/revoke')
  @ApiOperation({ summary: 'Admin: Revoke/expire a subscription immediately' })
  revokeSub(@Body() body: Record<string, any>) {
    const subId = body?.sub_id || body?.subId;
    return this.adminService.revokeSubscription(subId);
  }

  // ── Devices / HWID ────────────────────────────────────────────────────────
  @Get('devices')
  @ApiOperation({ summary: 'Admin: List all registered HWID device records' })
  getDevices() {
    return this.adminService.getAllDevices();
  }

  @Post('devices/ban')
  @ApiOperation({ summary: 'Admin: Ban a hardware device by HWID hash' })
  banDevice(@Body() body: Record<string, any>) {
    const hwidHash = body?.hwid_hash || body?.hwidHash;
    const reason = body?.reason;
    return this.adminService.banDevice(hwidHash, reason);
  }

  @Post('devices/unban')
  @ApiOperation({ summary: 'Admin: Unban a hardware device by HWID hash' })
  unbanDevice(@Body() body: Record<string, any>) {
    const hwidHash = body?.hwid_hash || body?.hwidHash;
    return this.adminService.unbanDevice(hwidHash);
  }

  // ── Relays ─────────────────────────────────────────────────────────────────
  @Get('relays')
  @ApiOperation({ summary: 'Admin: List all relay nodes' })
  getRelays() {
    return this.adminService.getAllRelays();
  }

  @Post('relays')
  @ApiOperation({ summary: 'Admin: Create a new relay node' })
  createRelay(@Body() body: any) {
    return this.adminService.createRelay(body);
  }

  @Patch('relays/:id')
  @ApiOperation({ summary: 'Admin: Update relay node settings' })
  updateRelay(@Param('id') id: string, @Body() body: any) {
    return this.adminService.updateRelay(id, body);
  }

  @Post('relays/:id/toggle')
  @ApiOperation({ summary: 'Admin: Toggle relay active status' })
  toggleRelay(@Param('id') id: string, @Body() body: Record<string, any>) {
    const isActive = body?.is_active !== undefined ? Boolean(body.is_active) : Boolean(body?.isActive);
    return this.adminService.toggleRelay(id, isActive);
  }

  @Delete('relays/:id')
  @ApiOperation({ summary: 'Admin: Delete a relay node' })
  deleteRelay(@Param('id') id: string) {
    return this.adminService.deleteRelay(id);
  }

  // ── Coupons ───────────────────────────────────────────────────────────────
  @Get('coupons')
  @ApiOperation({ summary: 'Admin: List all promo coupon codes' })
  getCoupons() {
    return this.adminService.getCoupons();
  }

  @Post('coupons/create')
  @ApiOperation({ summary: 'Admin: Create a new promo coupon code' })
  createCoupon(@Body() body: Record<string, any>) {
    const code = body?.code;
    const discountPct = Number(body?.discount_pct !== undefined ? body.discount_pct : body?.discountPct || 0);
    const maxUses = Number(body?.max_uses !== undefined ? body.max_uses : body?.maxUses || 100);
    const expiresAt = body?.expires_at || body?.expiresAt;
    return this.adminService.createCoupon({ code, discount_pct: discountPct, max_uses: maxUses, expires_at: expiresAt });
  }

  @Post('coupons/:id/deactivate')
  @ApiOperation({ summary: 'Admin: Deactivate a coupon (mark as exhausted)' })
  deactivateCoupon(@Param('id') id: string) {
    return this.adminService.deactivateCoupon(id);
  }

  // ── App Settings & Feature Flags ──────────────────────────────────────────
  @Get('app-settings')
  @ApiOperation({ summary: 'Admin: Get application configuration and feature flags' })
  getAppSettings() {
    return this.adminService.getAppSettings();
  }

  @Post('app-settings')
  @ApiOperation({ summary: 'Admin: Update application configuration and feature flags' })
  updateAppSettings(@Body() body: Record<string, any>) {
    return this.adminService.updateAppSettings(body);
  }

  @Post('users/manual-relay-access')
  @ApiOperation({ summary: 'Admin: Grant or revoke manual relay selection access for a specific user' })
  setUserManualRelayAccess(@Body() body: Record<string, any>) {
    const userId = body?.user_id || body?.userId;
    const canAccess = Boolean(body?.can_access !== undefined ? body.can_access : body?.canAccess);
    const remark = body?.remark;
    const actor = body?.actor || 'Admin';
    return this.adminService.setUserManualRelayAccess(userId, canAccess, remark, actor);
  }

  // ── OTA Releases ──────────────────────────────────────────────────────────
  @Get('releases')
  @ApiOperation({ summary: 'Admin: List all published OTA release records' })
  getReleases() {
    return this.adminService.getAllReleases();
  }

  @Post('releases')
  @ApiOperation({ summary: 'Admin: Publish a new OTA desktop release' })
  publishRelease(@Body() body: any) {
    return this.adminService.publishRelease(body);
  }

  @Patch('releases/:id')
  @ApiOperation({ summary: 'Admin: Update release flags (mandatory, active, etc.)' })
  updateRelease(@Param('id') id: string, @Body() body: any) {
    return this.adminService.updateRelease(id, body);
  }

  @Delete('releases/:id')
  @ApiOperation({ summary: 'Admin: Delete a release record' })
  deleteRelease(@Param('id') id: string) {
    return this.adminService.deleteRelease(id);
  }
}
