import {
  Entity,
  PrimaryGeneratedColumn,
  Column,
  CreateDateColumn,
  UpdateDateColumn,
  Index,
} from 'typeorm';

@Entity('user_sessions')
export class UserSessionEntity {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Index()
  @Column()
  userId: string;

  @Column()
  userEmail: string;

  @Column({ nullable: true })
  relayId: string;

  @Column({ nullable: true })
  relayName: string;

  @Column({ nullable: true })
  relayRegion: string;

  @Column({ nullable: true })
  relayHost: string;

  @Column({ nullable: true })
  gameName: string;

  @Column({ nullable: true })
  gameProcess: string;

  @Column({ type: 'int', default: 0 })
  pingMs: number;

  @Column({ type: 'float', default: 0 })
  downloadMbps: number;

  @Column({ type: 'float', default: 0 })
  uploadMbps: number;

  @Column({ name: 'bytes_sent', type: 'bigint', default: 0 })
  bytesSent: number;

  @Column({ name: 'bytes_received', type: 'bigint', default: 0 })
  bytesReceived: number;

  @Column({ nullable: true })
  clientIp: string;

  @Column({ nullable: true })
  clientVersion: string;

  @Column({ nullable: true })
  hwid: string;

  @Column({ default: true })
  isActive: boolean;

  @Column({ nullable: true })
  disconnectedAt: Date;

  @CreateDateColumn()
  connectedAt: Date;

  @UpdateDateColumn()
  lastHeartbeat: Date;
}
