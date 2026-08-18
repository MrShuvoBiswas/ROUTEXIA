import { Entity, PrimaryGeneratedColumn, Column, CreateDateColumn } from 'typeorm';

@Entity('relay_servers')
export class RelayEntity {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @Column({ name: 'region_code' })
  regionCode: string; // 'SG', 'IN', 'DXB'

  @Column({ name: 'display_name' })
  displayName: string; // 'Singapore 01 (AWS EC2)'

  @Column()
  host: string; // IP or domain

  @Column({ default: 9001 })
  port: number;

  @Column({ name: 'is_active', default: true })
  isActive: boolean;

  @Column({ default: 1 })
  priority: number;

  @Column({ name: 'max_capacity', default: 500 })
  maxCapacity: number;

  @Column({ name: 'current_load', default: 0 })
  currentLoad: number;

  @Column({ name: 'latency_ms', default: 40 })
  latencyMs: number;

  @Column({ default: 'Singapore' })
  city: string;

  @Column({ name: 'country_code', default: 'SG' })
  countryCode: string;

  @Column({ name: 'is_recommended', default: true })
  isRecommended: boolean;

  @Column({ name: 'cpu_usage', type: 'float', default: 0 })
  cpuUsage: number; // e.g. 15.2%

  @Column({ name: 'ram_usage', type: 'float', default: 0 })
  ramUsage: number; // e.g. 35.4%

  @Column({ name: 'ram_total_gb', type: 'float', default: 2.0 })
  ramTotalGb: number;

  @Column({ name: 'total_bytes_sent', type: 'bigint', default: 0 })
  totalBytesSent: number;

  @Column({ name: 'total_bytes_received', type: 'bigint', default: 0 })
  totalBytesReceived: number;

  @Column({ name: 'current_bandwidth_mbps', type: 'float', default: 0 })
  currentBandwidthMbps: number;

  @Column({ name: 'last_telemetry_at', nullable: true })
  lastTelemetryAt: Date;

  @CreateDateColumn({ name: 'created_at' })
  createdAt: Date;
}
