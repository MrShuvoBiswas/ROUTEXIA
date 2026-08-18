import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserEntity } from '../../entities/user.entity';
import { SubscriptionEntity } from '../../entities/subscription.entity';
import { RelayEntity } from '../../entities/relay.entity';
import { AppVersionEntity } from '../../entities/app-version.entity';
import { DeviceEntity } from '../../entities/device.entity';
import { CouponEntity } from '../../entities/coupon.entity';
import { UserSessionEntity } from '../../entities/user-session.entity';
import { UserHistoryEntity } from '../../entities/user-history.entity';
import { AdminService } from './admin.service';
import { AdminController } from './admin.controller';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      UserEntity,
      SubscriptionEntity,
      RelayEntity,
      AppVersionEntity,
      DeviceEntity,
      CouponEntity,
      UserSessionEntity,
      UserHistoryEntity,
    ]),
  ],
  controllers: [AdminController],
  providers: [AdminService],
  exports: [AdminService],
})
export class AdminModule {}
