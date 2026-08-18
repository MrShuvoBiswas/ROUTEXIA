import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsInt, IsNotEmpty, IsOptional, IsString, Max, Min } from 'class-validator';

export class AddRelayDto {
  @ApiProperty({ example: 'SG', description: 'Region code' })
  @IsString()
  @IsNotEmpty()
  regionCode: string;

  @ApiProperty({ example: 'Singapore 01 (AWS EC2)' })
  @IsString()
  @IsNotEmpty()
  displayName: string;

  @ApiProperty({ example: '3.1.31.201' })
  @IsString()
  @IsNotEmpty()
  host: string;

  @ApiProperty({ example: 9001, default: 9001 })
  @IsInt()
  @Min(1)
  @Max(65535)
  port: number;

  @ApiProperty({ example: 1, default: 1 })
  @IsOptional()
  @IsInt()
  priority?: number;

  @ApiProperty({ example: 500, default: 500 })
  @IsInt()
  maxCapacity: number;

  @ApiProperty({ example: 'Singapore' })
  @IsString()
  city: string;

  @ApiProperty({ example: 'SG' })
  @IsString()
  countryCode: string;

  @ApiProperty({ example: true, default: true })
  @IsBoolean()
  isRecommended: boolean;
}

export class UpdateRelayDto {
  @ApiProperty({ example: 'Singapore 01 (AWS EC2)' })
  @IsOptional()
  @IsString()
  displayName?: string;

  @ApiProperty({ example: '3.1.31.201' })
  @IsOptional()
  @IsString()
  host?: string;

  @ApiProperty({ example: 9001 })
  @IsOptional()
  @IsInt()
  port?: number;

  @ApiProperty({ example: true })
  @IsOptional()
  @IsBoolean()
  isActive?: boolean;

  @ApiProperty({ example: 500 })
  @IsOptional()
  @IsInt()
  maxCapacity?: number;

  @ApiProperty({ example: 45 })
  @IsOptional()
  @IsInt()
  currentLoad?: number;

  @ApiProperty({ example: true })
  @IsOptional()
  @IsBoolean()
  isRecommended?: boolean;
}

export class RelayTelemetryDto {
  @ApiProperty({ example: '3.1.31.201' })
  @IsString()
  host: string;

  @ApiProperty({ example: 9001, default: 9001 })
  @IsOptional()
  @IsInt()
  port?: number;

  @ApiProperty({ example: 12.5 })
  @IsOptional()
  cpuUsage?: number;

  @ApiProperty({ example: 34.2 })
  @IsOptional()
  ramUsage?: number;

  @ApiProperty({ example: 2.0 })
  @IsOptional()
  ramTotalGb?: number;

  @ApiProperty({ example: 10485760 })
  @IsOptional()
  totalBytesSent?: number;

  @ApiProperty({ example: 52428800 })
  @IsOptional()
  totalBytesReceived?: number;

  @ApiProperty({ example: 4.8 })
  @IsOptional()
  currentBandwidthMbps?: number;

  @ApiProperty({ example: 3 })
  @IsOptional()
  activeSessions?: number;
}
