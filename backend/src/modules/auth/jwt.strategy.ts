import { Injectable, UnauthorizedException, Logger } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { UserEntity } from '../../entities/user.entity';

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy) {
  private static readonly logger = new Logger('JwtStrategy');

  constructor(
    @InjectRepository(UserEntity)
    private userRepository: Repository<UserEntity>,
  ) {
    const secret = process.env.JWT_SECRET;
    if (!secret) {
      // Fail hard at startup — never allow a missing/default JWT secret in any environment
      JwtStrategy.logger.error(
        'FATAL: JWT_SECRET environment variable is not set. ' +
        'Set a strong random secret in your .env file before starting the server.',
      );
      throw new Error('JWT_SECRET environment variable is required but not set.');
    }

    super({
      jwtFromRequest: ExtractJwt.fromAuthHeaderAsBearerToken(),
      ignoreExpiration: false,
      secretOrKey: secret,
    });
  }

  async validate(payload: any) {
    const user = await this.userRepository.findOne({ where: { id: payload.sub } });
    if (!user) {
      throw new UnauthorizedException('User no longer exists');
    }
    if (user.isBanned) {
      throw new UnauthorizedException(`Account suspended: ${user.banReason || 'Violation of Terms'}`);
    }
    return user;
  }
}
