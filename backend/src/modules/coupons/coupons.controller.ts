import { Controller, Get, Post, Delete, Body, Param, UseGuards } from '@nestjs/common';
import { ApiTags, ApiOperation, ApiBearerAuth } from '@nestjs/swagger';
import { AuthGuard } from '@nestjs/passport';
import { CouponsService } from './coupons.service';
import { CreateCouponDto } from './dto/coupon.dto';
import { RolesGuard, Roles } from '../../guards/roles.guard';

@ApiTags('Admin Coupons')
@Controller('api/v1/admin/coupons')
export class CouponsController {
  constructor(private readonly couponsService: CouponsService) {}

  @Get()
  @ApiOperation({ summary: 'Admin: Get all promo discount coupons' })
  getAllCoupons() {
    return this.couponsService.getAllCoupons();
  }

  @Post()
  @ApiOperation({ summary: 'Admin: Create a new promotional coupon code' })
  createCoupon(@Body() dto: CreateCouponDto) {
    return this.couponsService.createCoupon(dto);
  }

  @Delete(':id')
  @ApiOperation({ summary: 'Admin: Delete coupon code' })
  deleteCoupon(@Param('id') id: string) {
    return this.couponsService.deleteCoupon(id);
  }
}
