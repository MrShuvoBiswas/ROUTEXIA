import { Module, OnModuleInit } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { TypeOrmModule } from '@nestjs/typeorm';
import { ServeStaticModule } from '@nestjs/serve-static';
import { join } from 'path';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import * as bcrypt from 'bcrypt';

import { UserEntity } from './entities/user.entity';
import { DeviceEntity } from './entities/device.entity';
import { SubscriptionEntity } from './entities/subscription.entity';
import { RelayEntity } from './entities/relay.entity';
import { CouponEntity } from './entities/coupon.entity';
import { AppVersionEntity } from './entities/app-version.entity';
import { UserSessionEntity } from './entities/user-session.entity';
import { UserHistoryEntity } from './entities/user-history.entity';

import { AuthModule } from './modules/auth/auth.module';
import { SessionsModule } from './modules/sessions/sessions.module';
import { UsersModule } from './modules/users/users.module';
import { RelaysModule } from './modules/relays/relays.module';
import { SubscriptionsModule } from './modules/subscriptions/subscriptions.module';
import { CouponsModule } from './modules/coupons/coupons.module';
import { VersionsModule } from './modules/versions/versions.module';
import { AdminModule } from './modules/admin/admin.module';

@Module({
  imports: [
    ConfigModule.forRoot({ isGlobal: true }),
    TypeOrmModule.forRootAsync({
      useFactory: () => {
        const dbUrl = process.env.DATABASE_URL || process.env.POSTGRES_URL;
        if (dbUrl) {
          return {
            type: 'postgres',
            url: dbUrl,
            ssl: dbUrl.includes('neon.tech') || dbUrl.includes('sslmode=require') || dbUrl.includes('postgres') ? { rejectUnauthorized: false } : false,
            extra: {
              max: 20,
              idleTimeoutMillis: 30000,
              connectionTimeoutMillis: 10000,
              keepAlive: true,
            },
            entities: [UserEntity, DeviceEntity, SubscriptionEntity, RelayEntity, CouponEntity, AppVersionEntity, UserSessionEntity, UserHistoryEntity],
            synchronize: true,
            autoLoadEntities: true,
          };
        }

        return {
          type: 'postgres',
          host: process.env.DB_HOST || 'localhost',
          port: parseInt(process.env.DB_PORT || '5432', 10),
          username: process.env.DB_USER || 'postgres',
          password: process.env.DB_PASSWORD || 'postgres',
          database: process.env.DB_NAME || 'routexia',
          ssl: process.env.DB_SSL === 'true' ? { rejectUnauthorized: false } : false,
          entities: [UserEntity, DeviceEntity, SubscriptionEntity, RelayEntity, CouponEntity, AppVersionEntity, UserSessionEntity, UserHistoryEntity],
          synchronize: true,
          autoLoadEntities: true,
        };
      },
    }),
    TypeOrmModule.forFeature([UserEntity, RelayEntity, AppVersionEntity]),
    ServeStaticModule.forRoot({
      rootPath: join(__dirname, '..', 'public'),
      serveRoot: '/admin',
    }),
    AuthModule,
    UsersModule,
    RelaysModule,
    SubscriptionsModule,
    CouponsModule,
    VersionsModule,
    AdminModule,
    SessionsModule,
  ],
})
export class AppModule implements OnModuleInit {
  constructor(
    @InjectRepository(UserEntity) private userRepo: Repository<UserEntity>,
    @InjectRepository(RelayEntity) private relayRepo: Repository<RelayEntity>,
    @InjectRepository(AppVersionEntity) private versionRepo: Repository<AppVersionEntity>,
  ) {}

  async onModuleInit() {
    // Admin account seeding on startup if not exists
    const adminEmail = process.env.ADMIN_EMAIL ? process.env.ADMIN_EMAIL.toLowerCase() : '';
    if (adminEmail) {
      let admin = await this.userRepo.findOne({ where: { email: adminEmail } });
      if (!admin) {
        const pass = process.env.ADMIN_PASSWORD || '';
        const hash = await bcrypt.hash(pass, 10);
        admin = this.userRepo.create({
          email: adminEmail,
          passwordHash: hash,
          role: 'admin',
          referralCode: 'RX-ADMIN',
        });
        await this.userRepo.save(admin);
        console.log(`[SEED] Created default Admin account: ${adminEmail}`);
      }
    }

    // Seed initial app version if empty
    const verCount = await this.versionRepo.count();
    if (verCount === 0) {
      await this.versionRepo.save({
        version: '2.0.0',
        releaseNotes: 'RouteXia v2.0 Enterprise Multipath Engine & Process WFP Filter',
        downloadUrl: 'https://routexia.com/downloads/RouteXia-v2.0.0.exe',
        checksumSha256: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
        isMandatory: true,
      });
      console.log(`[SEED] Seeded initial App Version v2.0.0`);
    }
  }
}
