import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { RelayEntity } from '../../entities/relay.entity';
import { UserSessionEntity } from '../../entities/user-session.entity';
import { RelaysService } from './relays.service';
import { RelaysController } from './relays.controller';

@Module({
  imports: [TypeOrmModule.forFeature([RelayEntity, UserSessionEntity])],
  controllers: [RelaysController],
  providers: [RelaysService],
  exports: [RelaysService],
})
export class RelaysModule {}
