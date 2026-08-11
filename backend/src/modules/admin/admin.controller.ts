import { Controller, Get, UseGuards } from '@nestjs/common';
import { ApiTags, ApiOperation, ApiBearerAuth } from '@nestjs/swagger';
import { AuthGuard } from '@nestjs/passport';
import { AdminService } from './admin.service';
import { RolesGuard, Roles } from '../../guards/roles.guard';

@ApiTags('Admin Stats')
@Controller('api/v1/admin/stats')
export class AdminController {
  constructor(private readonly adminService: AdminService) {}

  @Get()
  @ApiOperation({ summary: 'Admin: Get aggregated system overview statistics' })
  getStats() {
    return this.adminService.getAdminStats();
  }
}
