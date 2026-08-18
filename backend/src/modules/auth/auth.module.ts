import { Module } from '@nestjs/common';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { TypeOrmModule } from '@nestjs/typeorm';
import { UserEntity } from '../../entities/user.entity';
import { DeviceEntity } from '../../entities/device.entity';
import { SubscriptionEntity } from '../../entities/subscription.entity';
import { RelayEntity } from '../../entities/relay.entity';
import { UserHistoryEntity } from '../../entities/user-history.entity';
import { AuthService } from './auth.service';
import { AuthController } from './auth.controller';
import { JwtStrategy } from './jwt.strategy';
import { FirebaseAdminService } from './firebase-admin.service';

@Module({
  imports: [
    TypeOrmModule.forFeature([UserEntity, DeviceEntity, SubscriptionEntity, RelayEntity, UserHistoryEntity]),
    PassportModule,
    JwtModule.register({
      secret: process.env.JWT_SECRET || 'RouteXia_Secret_Key_2026_Enterprise_Secure',
      signOptions: { expiresIn: '30d' },
    }),
  ],
  controllers: [AuthController],
  providers: [AuthService, JwtStrategy, FirebaseAdminService],
  exports: [AuthService, JwtModule, PassportModule, FirebaseAdminService],
})
export class AuthModule {}
