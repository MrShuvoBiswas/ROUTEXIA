import { Entity, PrimaryGeneratedColumn, Column, CreateDateColumn, ManyToOne, JoinColumn } from 'typeorm';
import { UserEntity } from './user.entity';

@Entity('user_history')
export class UserHistoryEntity {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ name: 'user_id' })
  userId: string;

  @Column({ name: 'action_type' })
  actionType: string; // 'TRIAL_STARTED' | 'TRIAL_EXTENDED' | 'TRIAL_REDUCED' | 'TRIAL_TERMINATED' | 'SUB_GRANTED' | 'SUB_REVOKED' | 'DISCOUNT_SET' | 'ACCOUNT_BANNED' | 'ACCOUNT_UNBANNED' | 'ACCOUNT_DELETED' | 'ACCOUNT_RESTORED' | 'PAYMENT_RECEIVED'

  @Column({ name: 'title' })
  title: string;

  @Column({ name: 'details', default: '' })
  details: string;

  @Column({ name: 'days_delta', type: 'int', default: 0 })
  daysDelta: number;

  @Column({ name: 'actor', default: 'Admin' })
  actor: string;

  @Column({ name: 'remark', default: '' })
  remark: string;

  @CreateDateColumn({ name: 'created_at' })
  createdAt: Date;

  @ManyToOne(() => UserEntity, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'user_id' })
  user: UserEntity;
}
