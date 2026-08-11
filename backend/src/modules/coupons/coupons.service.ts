import { Injectable, BadRequestException, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { CouponEntity } from '../../entities/coupon.entity';
import { CreateCouponDto } from './dto/coupon.dto';

@Injectable()
export class CouponsService {
  constructor(
    @InjectRepository(CouponEntity)
    private couponRepository: Repository<CouponEntity>,
  ) {}

  async getAllCoupons() {
    return this.couponRepository.find({ order: { createdAt: 'DESC' } });
  }

  async createCoupon(dto: CreateCouponDto) {
    const existing = await this.couponRepository.findOne({ where: { code: dto.code.toUpperCase() } });
    if (existing) {
      throw new BadRequestException(`Coupon code ${dto.code} already exists`);
    }

    const coupon = this.couponRepository.create({
      code: dto.code.toUpperCase(),
      discountPct: dto.discountPct,
      maxUses: dto.maxUses || 100,
      usedCount: 0,
      expiresAt: dto.expiresAt ? new Date(dto.expiresAt) : null,
    });

    return this.couponRepository.save(coupon);
  }

  async deleteCoupon(id: string) {
    const coupon = await this.couponRepository.findOne({ where: { id } });
    if (!coupon) {
      throw new NotFoundException(`Coupon with ID ${id} not found`);
    }
    await this.couponRepository.remove(coupon);
    return { success: true, message: `Coupon ${coupon.code} deleted` };
  }
}
