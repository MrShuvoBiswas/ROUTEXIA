import { ApiProperty } from '@nestjs/swagger';
import { IsEmail, IsNotEmpty, IsOptional, IsString, MinLength } from 'class-validator';

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

export class FirebaseLoginDto {
  @ApiProperty({
    description:
      'Firebase ID Token — obtained from Firebase Authentication on the WPF client ' +
      'after email/password or Google sign-in. This token is verified server-side ' +
      'using Firebase Admin SDK.',
    example: 'eyJhbGciOiJSUzI1NiIsImtpZCI6...',
  })
  @IsString()
  @IsNotEmpty()
  id_token: string;

  @ApiProperty({
    description: 'Hardware ID of the client PC (for anti-abuse trial tracking)',
    example: 'HWID-9A8F-4B12-7721',
  })
  @IsString()
  @IsNotEmpty()
  hwid: string;

  @ApiProperty({
    description: 'Optional email hint (used as fallback if not present in the Firebase token)',
    example: 'user@gmail.com',
    required: false,
  })
  @IsEmail()
  @IsOptional()
  email?: string;
}
