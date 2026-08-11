import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsNotEmpty, IsOptional, IsString } from 'class-validator';

export class PublishVersionDto {
  @ApiProperty({ example: '2.1.0', description: 'Semantic version number' })
  @IsString()
  @IsNotEmpty()
  version: string;

  @ApiProperty({ example: 'Fixed Singapura relay latency. Added WFP bypass module.' })
  @IsString()
  @IsNotEmpty()
  releaseNotes: string;

  @ApiProperty({ example: 'https://routexia.com/downloads/RouteXia-v2.1.0.exe' })
  @IsString()
  @IsNotEmpty()
  downloadUrl: string;

  @ApiProperty({ example: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', required: false })
  @IsOptional()
  @IsString()
  checksumSha256?: string;

  @ApiProperty({ example: true, default: false })
  @IsOptional()
  @IsBoolean()
  isMandatory?: boolean;

  @ApiProperty({ example: '1.0.0', default: '1.0.0' })
  @IsOptional()
  @IsString()
  minSupportedVersion?: string;

  @ApiProperty({ example: true, default: true })
  @IsOptional()
  @IsBoolean()
  silentUpdate?: boolean;
}
