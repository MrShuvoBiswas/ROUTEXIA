import { Controller, Post, Body, UseGuards } from '@nestjs/common';
import { ApiTags, ApiOperation, ApiBearerAuth } from '@nestjs/swagger';
import { AuthGuard } from '@nestjs/passport';
import { SubscriptionsService } from './subscriptions.service';
import { ExtendSubscriptionDto } from './dto/subscription.dto';
import { RolesGuard, Roles } from '../../guards/roles.guard';

@ApiTags('Admin Subscriptions')
@Controller('api/v1/admin/subscriptions')
export class SubscriptionsController {
  constructor(private readonly subService: SubscriptionsService) {}

  @Post('extend')
  @ApiOperation({ summary: 'Admin: Extend or grant subscription days to user' })
  extendSubscription(@Body() dto: ExtendSubscriptionDto) {
    return this.subService.extendSubscription(dto);
  }
}
