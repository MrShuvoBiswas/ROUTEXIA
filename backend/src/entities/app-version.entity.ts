import { Entity, PrimaryGeneratedColumn, Column, CreateDateColumn } from 'typeorm';

@Entity('app_versions')
export class AppVersionEntity {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column()
  version: string; // e.g. "2.0.0"

  @Column({ name: 'release_notes', type: 'text' })
  releaseNotes: string;

  @Column({ name: 'download_url' })
  downloadUrl: string;

  @Column({ name: 'checksum_sha256', default: '' })
  checksumSha256: string;

  @Column({ name: 'is_mandatory', default: false })
  isMandatory: boolean;

  @Column({ name: 'min_supported_version', default: '1.0.0' })
  minSupportedVersion: string;

  @Column({ name: 'silent_update', default: true })
  silentUpdate: boolean;

  @Column({ name: 'is_active', default: true })
  isActive: boolean;

  @Column({ name: 'file_size_bytes', type: 'bigint', default: 0 })
  fileSizeBytes: number;

  @CreateDateColumn({ name: 'created_at' })
  createdAt: Date;
}
