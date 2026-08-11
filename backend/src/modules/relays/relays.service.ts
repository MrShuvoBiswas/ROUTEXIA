import { Injectable, NotFoundException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { RelayEntity } from '../../entities/relay.entity';
import { AddRelayDto, UpdateRelayDto } from './dto/relay.dto';

@Injectable()
export class RelaysService {
  constructor(
    @InjectRepository(RelayEntity)
    private relayRepository: Repository<RelayEntity>,
  ) {}

  async getActiveRelays() {
    const relays = await this.relayRepository.find({
      order: { priority: 'ASC' },
    });

    return relays.map((r) => {
      const loadPct = r.maxCapacity > 0 ? Math.round((r.currentLoad / r.maxCapacity) * 100) : 0;
      return {
        id: r.id,
        region_code: r.regionCode,
        display_name: r.displayName,
        host: r.host,
        port: r.port,
        priority: r.priority,
        is_active: r.isActive,
        max_capacity: r.maxCapacity,
        current_load: r.currentLoad,
        latency_ms: r.latencyMs,
        city: r.city,
        country_code: r.countryCode,
        is_recommended: r.isRecommended,
        load_percent: loadPct,
        high_load_alert: loadPct > 80,
      };
    });
  }

  async addRelay(dto: AddRelayDto) {
    const relay = this.relayRepository.create({
      regionCode: dto.regionCode,
      displayName: dto.displayName,
      host: dto.host,
      port: dto.port || 9001,
      priority: dto.priority || 1,
      maxCapacity: dto.maxCapacity || 500,
      city: dto.city || 'Singapore',
      countryCode: dto.countryCode || 'SG',
      isRecommended: dto.isRecommended !== undefined ? dto.isRecommended : true,
    });
    return this.relayRepository.save(relay);
  }

  async updateRelay(id: string, dto: UpdateRelayDto) {
    const relay = await this.relayRepository.findOne({ where: { id } });
    if (!relay) {
      throw new NotFoundException(`Relay server with ID ${id} not found`);
    }
    Object.assign(relay, dto);
    return this.relayRepository.save(relay);
  }

  async deleteRelay(id: string) {
    const relay = await this.relayRepository.findOne({ where: { id } });
    if (!relay) {
      throw new NotFoundException(`Relay server with ID ${id} not found`);
    }
    await this.relayRepository.remove(relay);
    return { success: true, message: `Relay ${id} removed successfully` };
  }

  async clearAllRelays() {
    await this.relayRepository.clear();
    return { success: true, message: 'All relay servers cleared from inventory' };
  }
}
