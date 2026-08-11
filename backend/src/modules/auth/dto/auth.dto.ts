import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsNotEmpty, IsString, MinLength } from 'class-validator';

export class RegisterDto {
  @ApiProperty({ example: 'user@routexia.com', description: 'User email address' })
  @IsEmail()
  email: string;

  @ApiProperty({ example: 'Password123!', description: 'User password (min 6 chars)' })
  @IsString()
  @MinLength(6)
  password: string;

  @ApiProperty({ example: 'HWID-9A8F-4B12-7721', description: 'Hardware unique ID of PC' })
  @IsString()
  @IsNotEmpty()
  hwid: string;
}

export class LoginDto {
  @ApiProperty({ example: 'user@routexia.com' })
  @IsEmail()
  email: string;

  @ApiProperty({ example: 'Password123!' })
  @IsString()
  password: string;

  @ApiProperty({ example: 'HWID-9A8F-4B12-7721' })
  @IsString()
  @IsNotEmpty()
  hwid: string;
}
