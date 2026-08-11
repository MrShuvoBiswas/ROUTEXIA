import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { AppVersionEntity } from '../../entities/app-version.entity';
import { VersionsService } from './versions.service';
import { VersionsController } from './versions.controller';

@Module({
  imports: [TypeOrmModule.forFeature([AppVersionEntity])],
  controllers: [VersionsController],
  providers: [VersionsService],
  exports: [VersionsService],
})
export class VersionsModule {}
