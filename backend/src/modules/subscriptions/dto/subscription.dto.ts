import { ApiProperty } from '@nestjs/swagger';
import { IsInt, IsNotEmpty, IsString, Min } from 'class-validator';

export class ExtendSubscriptionDto {
  @ApiProperty({ example: 'usr-1234' })
  @IsString()
  @IsNotEmpty()
  userId: string;

  @ApiProperty({ example: 'monthly', description: 'trial | monthly | quarterly | yearly' })
  @IsString()
  @IsNotEmpty()
  planType: string;

  @ApiProperty({ example: 30, description: 'Number of days to extend' })
  @IsInt()
  @Min(1)
  days: number;
}
