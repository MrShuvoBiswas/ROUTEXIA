import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { CouponEntity } from '../../entities/coupon.entity';
import { CouponsService } from './coupons.service';

@Module({
  imports: [TypeOrmModule.forFeature([CouponEntity])],
  controllers: [],
  providers: [CouponsService],
  exports: [CouponsService],
})
export class CouponsModule {}
