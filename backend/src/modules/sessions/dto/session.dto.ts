import { IsOptional, IsString, IsNumber, IsUUID } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';

export class SessionConnectDto {
  @ApiProperty() @IsUUID() userId: string;
  @ApiProperty() @IsString() relayId: string;
  @ApiProperty() @IsString() relayName: string;
  @ApiProperty() @IsString() relayRegion: string;
  @ApiProperty() @IsString() relayHost: string;
  @ApiProperty() @IsOptional() @IsString() gameName?: string;
  @ApiProperty() @IsOptional() @IsString() gameProcess?: string;
  @ApiProperty() @IsOptional() @IsNumber() pingMs?: number;
  @ApiProperty() @IsOptional() @IsString() hwid?: string;
  @ApiProperty() @IsOptional() @IsString() clientVersion?: string;
}

export class SessionHeartbeatDto {
  @ApiProperty() @IsUUID() sessionId: string;
  @ApiProperty() @IsOptional() @IsNumber() pingMs?: number;
  @ApiProperty() @IsOptional() @IsNumber() downloadMbps?: number;
  @ApiProperty() @IsOptional() @IsNumber() uploadMbps?: number;
  @ApiProperty() @IsOptional() @IsNumber() bytesSent?: number;
  @ApiProperty() @IsOptional() @IsNumber() bytesReceived?: number;
  @ApiProperty() @IsOptional() @IsString() gameName?: string;
  @ApiProperty() @IsOptional() @IsString() gameProcess?: string;
}

export class SessionDisconnectDto {
  @ApiProperty() @IsUUID() sessionId: string;
  @ApiProperty() @IsOptional() @IsNumber() bytesSent?: number;
  @ApiProperty() @IsOptional() @IsNumber() bytesReceived?: number;
}
