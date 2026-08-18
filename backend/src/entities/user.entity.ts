import { Entity, PrimaryGeneratedColumn, Column, CreateDateColumn, UpdateDateColumn, OneToMany } from 'typeorm';
import { SubscriptionEntity } from './subscription.entity';

@Entity('users')
export class UserEntity {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ unique: true })
  email: string;

  // nullable for Firebase-authenticated users who never set a local password
  @Column({ name: 'password_hash', nullable: true, default: null })
  passwordHash: string | null;

  // Firebase UID — set when user signs in via Firebase Auth
  @Column({ name: 'firebase_uid', nullable: true, unique: true, default: null })
  firebaseUid: string | null;

  // Whether the account was created / managed via Firebase Auth
  @Column({ name: 'is_firebase_user', default: false })
  isFirebaseUser: boolean;

  @Column({ default: 'user' })
  role: string; // 'admin' | 'user'

  @Column({ name: 'is_banned', default: false })
  isBanned: boolean;

  @Column({ name: 'ban_reason', default: '' })
  banReason: string;

  @Column({ name: 'is_deleted', default: false })
  isDeleted: boolean;

  @Column({ name: 'deleted_at', type: 'timestamp', nullable: true, default: null })
  deletedAt: Date | null;

  @Column({ name: 'custom_discount_pct', default: 0 })
  customDiscountPct: number;

  @Column({ name: 'can_manual_select_relay', default: false })
  canManualSelectRelay: boolean;

  @Column({ name: 'referral_code', nullable: true, unique: true })
  referralCode: string;

  @Column({ name: 'referred_by', default: '' })
  referredBy: string;

  @CreateDateColumn({ name: 'created_at' })
  createdAt: Date;

  @UpdateDateColumn({ name: 'updated_at' })
  updatedAt: Date;

  @OneToMany(() => SubscriptionEntity, (sub) => sub.user)
  subscriptions: SubscriptionEntity[];
}
