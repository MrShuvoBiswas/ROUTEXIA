import { Controller, Get, Post, Put, Delete, Body, Param, Query, UseGuards } from '@nestjs/common';
import { ApiTags, ApiOperation, ApiBearerAuth } from '@nestjs/swagger';
import { AuthGuard } from '@nestjs/passport';
import { VersionsService } from './versions.service';
import { PublishVersionDto } from './dto/version.dto';
import { RolesGuard, Roles } from '../../guards/roles.guard';

@ApiTags('App Version')
@Controller('api/v1/app/version')
export class VersionsController {
  constructor(private readonly versionsService: VersionsService) {}

  @Get()
  @ApiOperation({ summary: 'Get latest application version info for auto-updater' })
  getLatestVersion() {
    return this.versionsService.getLatestVersion();
  }

  @Get('history')
  @ApiOperation({ summary: 'Admin: Get full OTA version release history' })
  getAllVersions() {
    return this.versionsService.getAllVersions();
  }

  @Post()
  @ApiOperation({ summary: 'Admin: Push/Publish new client application release' })
  publishVersion(@Body() dto: PublishVersionDto) {
    return this.versionsService.publishVersion(dto);
  }

  @Put(':id/toggle')
  @ApiOperation({ summary: 'Admin: Toggle release active status (Enable/Disable/Rollback)' })
  toggleActive(@Param('id') id: string, @Query('active') active: string) {
    return this.versionsService.toggleActive(id, active === 'true');
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Admin: Delete release record' })
  deleteVersion(@Param('id') id: string) {
    return this.versionsService.deleteVersion(id);
  }
}
