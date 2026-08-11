import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { AppVersionEntity } from '../../entities/app-version.entity';
import { PublishVersionDto } from './dto/version.dto';

@Injectable()
export class VersionsService {
  constructor(
    @InjectRepository(AppVersionEntity)
    private versionRepository: Repository<AppVersionEntity>,
  ) {}

  async getLatestVersion() {
    const version = await this.versionRepository.findOne({
      where: { isActive: true },
      order: { createdAt: 'DESC' },
    });

    if (!version) {
      return {
        id: 'default',
        version: '1.0.0',
        release_notes: 'Initial release',
        download_url: 'https://routexia.com/downloads/RouteXia-v1.0.0.exe',
        checksum_sha256: '',
        is_mandatory: false,
        min_supported_version: '1.0.0',
        silent_update: true,
        created_at: new Date(),
      };
    }

    return {
      id: version.id,
      version: version.version,
      release_notes: version.releaseNotes,
      download_url: version.downloadUrl,
      checksum_sha256: version.checksumSha256,
      is_mandatory: version.isMandatory,
      min_supported_version: version.minSupportedVersion || '1.0.0',
      silent_update: version.silentUpdate,
      file_size_bytes: version.fileSizeBytes,
      created_at: version.createdAt,
    };
  }

  async getAllVersions() {
    return this.versionRepository.find({ order: { createdAt: 'DESC' } });
  }

  async publishVersion(dto: PublishVersionDto) {
    const version = this.versionRepository.create({
      version: dto.version,
      releaseNotes: dto.releaseNotes,
      downloadUrl: dto.downloadUrl,
      checksumSha256: dto.checksumSha256 || '',
      isMandatory: dto.isMandatory ?? false,
      minSupportedVersion: dto.minSupportedVersion || '1.0.0',
      silentUpdate: dto.silentUpdate ?? true,
      isActive: true,
    });
    return this.versionRepository.save(version);
  }

  async toggleActive(id: string, isActive: boolean) {
    const version = await this.versionRepository.findOne({ where: { id } });
    if (!version) {
      throw new NotFoundException(`Release version ${id} not found`);
    }
    version.isActive = isActive;
    return this.versionRepository.save(version);
  }

  async deleteVersion(id: string) {
    const version = await this.versionRepository.findOne({ where: { id } });
    if (!version) {
      throw new NotFoundException(`Release version ${id} not found`);
    }
    await this.versionRepository.remove(version);
    return { success: true, message: `Version ${version.version} deleted` };
  }
}
