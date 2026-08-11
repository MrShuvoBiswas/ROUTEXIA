import { ApiProperty } from '@nestjs/swagger';
import { IsDateString, IsInt, IsNotEmpty, IsOptional, IsString, Max, Min } from 'class-validator';

export class CreateCouponDto {
  @ApiProperty({ example: 'PUBGBD2026', description: 'Promo coupon code' })
  @IsString()
  @IsNotEmpty()
  code: string;

  @ApiProperty({ example: 25, description: 'Percentage discount (1-100)' })
  @IsInt()
  @Min(1)
  @Max(100)
  discountPct: number;

  @ApiProperty({ example: 100, default: 100 })
  @IsInt()
  @Min(1)
  maxUses: number;

  @ApiProperty({ example: '2026-12-31T23:59:59Z', required: false })
  @IsOptional()
  @IsDateString()
  expiresAt?: string;
}
