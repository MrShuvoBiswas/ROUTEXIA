import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { UserEntity } from '../../entities/user.entity';
import { BanUserDto, CustomDiscountDto } from './dto/user-admin.dto';

@Injectable()
export class UsersService {
  constructor(
    @InjectRepository(UserEntity)
    private userRepository: Repository<UserEntity>,
  ) {}

  async getAllUsers() {
    const users = await this.userRepository.find({
      relations: ['subscriptions'],
      order: { createdAt: 'DESC' },
    });

    return users.map((u) => {
      const activeSub = u.subscriptions?.find((s) => s.status === 'active' && new Date(s.expiresAt) > new Date());
      return {
        id: u.id,
        email: u.email,
        role: u.role,
        is_banned: u.isBanned,
        ban_reason: u.banReason,
        custom_discount_pct: u.customDiscountPct,
        referral_code: u.referralCode,
        referred_by: u.referredBy,
        created_at: u.createdAt,
        active_plan: activeSub ? activeSub.planType : 'none',
        expires_at: activeSub ? activeSub.expiresAt : null,
      };
    });
  }

  async banUser(dto: BanUserDto) {
    const user = await this.userRepository.findOne({ where: { id: dto.userId } });
    if (!user) {
      throw new NotFoundException(`User ${dto.userId} not found`);
    }
    user.isBanned = dto.isBanned;
    user.banReason = dto.isBanned ? dto.reason || 'Banned by Administrator' : '';
    await this.userRepository.save(user);
    return { success: true, message: `User ${user.email} ban status updated to ${user.isBanned}` };
  }

  async setUserDiscount(dto: CustomDiscountDto) {
    const user = await this.userRepository.findOne({ where: { id: dto.userId } });
    if (!user) {
      throw new NotFoundException(`User ${dto.userId} not found`);
    }
    user.customDiscountPct = dto.discountPct;
    await this.userRepository.save(user);
    return { success: true, message: `Custom discount for ${user.email} set to ${dto.discountPct}%` };
  }
}
