import { ApiProperty } from '@nestjs/swagger';
import { IsBoolean, IsInt, IsNotEmpty, IsOptional, IsString, Max, Min } from 'class-validator';

export class BanUserDto {
  @ApiProperty({ example: 'usr-1234' })
  @IsString()
  @IsNotEmpty()
  userId: string;

  @ApiProperty({ example: true })
  @IsBoolean()
  isBanned: boolean;

  @ApiProperty({ example: 'Payment chargeback / Multi-account abuse' })
  @IsOptional()
  @IsString()
  reason?: string;
}

export class CustomDiscountDto {
  @ApiProperty({ example: 'usr-1234' })
  @IsString()
  @IsNotEmpty()
  userId: string;

  @ApiProperty({ example: 20, description: 'Percentage discount (0-100)' })
  @IsInt()
  @Min(0)
  @Max(100)
  discountPct: number;
}
