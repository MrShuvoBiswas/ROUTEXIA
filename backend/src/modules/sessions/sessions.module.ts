import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserSessionEntity } from '../../entities/user-session.entity';
import { UserEntity } from '../../entities/user.entity';
import { SubscriptionEntity } from '../../entities/subscription.entity';
import { DeviceEntity } from '../../entities/device.entity';
import { SessionsService } from './sessions.service';
import { SessionsController } from './sessions.controller';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      UserSessionEntity,
      UserEntity,
      SubscriptionEntity,
      DeviceEntity,
    ]),
  ],
  controllers: [SessionsController],
  providers: [SessionsService],
  exports: [SessionsService],
})
export class SessionsModule {}
