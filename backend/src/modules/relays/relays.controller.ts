import { Controller, Get, Post, Put, Delete, Body, Param, UseGuards } from '@nestjs/common';
import { ApiTags, ApiOperation, ApiBearerAuth } from '@nestjs/swagger';
import { AuthGuard } from '@nestjs/passport';
import { RelaysService } from './relays.service';
import { AddRelayDto, UpdateRelayDto } from './dto/relay.dto';
import { RolesGuard, Roles } from '../../guards/roles.guard';

@ApiTags('Relays')
@Controller('api/v1/relays')
export class RelaysController {
  constructor(private readonly relaysService: RelaysService) {}

  @Get()
  @ApiOperation({ summary: 'Get active relay servers list for desktop client' })
  getRelays() {
    return this.relaysService.getActiveRelays();
  }

  @Post('telemetry')
  @ApiOperation({ summary: 'Relay Server: Report live hardware & traffic telemetry' })
  reportTelemetry(@Body() dto: import('./dto/relay.dto').RelayTelemetryDto) {
    return this.relaysService.reportTelemetry(dto);
  }

  @Post()
  @ApiOperation({ summary: 'Admin: Add a new relay server' })
  addRelay(@Body() dto: AddRelayDto) {
    return this.relaysService.addRelay(dto);
  }

  @Put(':id')
  @ApiOperation({ summary: 'Admin: Update existing relay server specs or load' })
  updateRelay(@Param('id') id: string, @Body() dto: UpdateRelayDto) {
    return this.relaysService.updateRelay(id, dto);
  }

  @Delete('clear/all')
  @ApiOperation({ summary: 'Admin: Clear all dummy relay servers' })
  clearAllRelays() {
    return this.relaysService.clearAllRelays();
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Admin: Delete relay server' })
  deleteRelay(@Param('id') id: string) {
    return this.relaysService.deleteRelay(id);
  }
}
