import { Controller, Post, Body, HttpCode, HttpStatus } from '@nestjs/common';
import { ApiTags, ApiOperation, ApiResponse } from '@nestjs/swagger';
import { AuthService } from './auth.service';
import { RegisterDto, LoginDto } from './dto/auth.dto';

@ApiTags('Auth')
@Controller('api/v1/auth')
export class AuthController {
  constructor(private readonly authService: AuthService) {}

  @Post('register')
  @ApiOperation({ summary: 'Register new user account with HWID trial anti-abuse' })
  @ApiResponse({ status: 201, description: 'User registered successfully with trial subscription' })
  register(@Body() dto: RegisterDto) {
    return this.authService.register(dto);
  }

  @Post('login')
  @HttpCode(HttpStatus.OK)
  @ApiOperation({ summary: 'Login user with email, password & HWID' })
  @ApiResponse({ status: 200, description: 'Login successful, returns JWT & subscription status' })
  login(@Body() dto: LoginDto) {
    return this.authService.login(dto);
  }
}
