import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { SubscriptionEntity } from '../../entities/subscription.entity';
import { UserEntity } from '../../entities/user.entity';
import { ExtendSubscriptionDto } from './dto/subscription.dto';

@Injectable()
export class SubscriptionsService {
  constructor(
    @InjectRepository(SubscriptionEntity)
    private subRepository: Repository<SubscriptionEntity>,
    @InjectRepository(UserEntity)
    private userRepository: Repository<UserEntity>,
  ) {}

  async extendSubscription(dto: ExtendSubscriptionDto) {
    const user = await this.userRepository.findOne({ where: { id: dto.userId } });
    if (!user) {
      throw new NotFoundException(`User ${dto.userId} not found`);
    }

    let sub = await this.subRepository.findOne({
      where: { userId: dto.userId },
      order: { expiresAt: 'DESC' },
    });

    const now = new Date();
    let baseDate = now;

    if (sub && new Date(sub.expiresAt) > now) {
      baseDate = new Date(sub.expiresAt);
    }

    const newExpiresAt = new Date(baseDate.getTime() + dto.days * 24 * 60 * 60 * 1000);

    if (sub) {
      sub.planType = dto.planType;
      sub.status = 'active';
      sub.expiresAt = newExpiresAt;
    } else {
      sub = this.subRepository.create({
        userId: user.id,
        hwidHash: 'MANUAL-ADMIN-GRANT',
        planType: dto.planType,
        status: 'active',
        startsAt: now,
        expiresAt: newExpiresAt,
      });
    }

    await this.subRepository.save(sub);

    return {
      success: true,
      message: `Extended ${user.email} subscription by ${dto.days} days`,
      subscription: sub,
    };
  }
}
