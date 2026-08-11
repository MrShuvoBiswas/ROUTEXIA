import { Entity, PrimaryColumn, Column, CreateDateColumn } from 'typeorm';

@Entity('devices')
export class DeviceEntity {
  @PrimaryColumn({ name: 'hwid_hash' })
  hwidHash: string;

  @Column({ name: 'first_user_id' })
  firstUserId: string;

  @Column({ name: 'trial_claimed', default: true })
  trialClaimed: boolean;

  @Column({ name: 'is_banned', default: false })
  isBanned: boolean;

  @Column({ name: 'ban_reason', default: '' })
  banReason: string;

  @CreateDateColumn({ name: 'first_claimed_at' })
  firstClaimedAt: Date;
}
