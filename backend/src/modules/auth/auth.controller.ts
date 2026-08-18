import {
  Controller,
  Post,
  Get,
  Body,
  HttpCode,
  HttpStatus,
  Headers,
  UnauthorizedException,
} from '@nestjs/common';
import { ApiTags, ApiOperation, ApiResponse } from '@nestjs/swagger';
import { AuthService } from './auth.service';
import { RegisterDto, LoginDto, FirebaseLoginDto } from './dto/auth.dto';
import { JwtService } from '@nestjs/jwt';

@ApiTags('Auth')
@Controller()
export class AuthController {
  constructor(
    private readonly authService: AuthService,
    private readonly jwtService: JwtService,
  ) {}

  @Post('api/v1/auth/register')
  @ApiOperation({ summary: 'Register new user account with HWID trial anti-abuse' })
  @ApiResponse({ status: 201, description: 'User registered successfully with trial subscription' })
  register(@Body() dto: RegisterDto) {
    return this.authService.register(dto);
  }

  @Post('api/v1/auth/login')
  @HttpCode(HttpStatus.OK)
  @ApiOperation({ summary: 'Login user with email, password & HWID' })
  @ApiResponse({ status: 200, description: 'Login successful, returns JWT & subscription status' })
  login(@Body() dto: LoginDto) {
    return this.authService.login(dto);
  }

  @Post('api/v1/auth/firebase')
  @HttpCode(HttpStatus.OK)
  @ApiOperation({
    summary: 'Authenticate using a Firebase ID Token (Email/Password or Google OAuth)',
  })
  @ApiResponse({ status: 200, description: 'Firebase authentication successful' })
  @ApiResponse({ status: 401, description: 'Invalid or expired Firebase ID token' })
  loginWithFirebase(@Body() dto: FirebaseLoginDto) {
    return this.authService.loginWithFirebase(dto);
  }

  @Post('api/v1/auth/forgot-password')
  @HttpCode(HttpStatus.OK)
  @ApiOperation({
    summary: 'Generate custom branded password reset link for RouteXia',
  })
  @ApiResponse({ status: 200, description: 'Reset link generated successfully' })
  forgotPassword(@Body('email') email: string) {
    return this.authService.forgotPassword(email);
  }

  @Get('api/v1/auth/profile')
  @Get('api/v1/user/profile')
  @ApiOperation({ summary: 'Get current user profile & real-time subscription status' })
  @ApiResponse({ status: 200, description: 'Returns real-time user and subscription profile' })
  @ApiResponse({ status: 401, description: 'Unauthorized, account banned or deleted' })
  async getProfile(@Headers('authorization') authHeader?: string) {
    if (!authHeader || !authHeader.startsWith('Bearer ')) {
      throw new UnauthorizedException('Missing authorization token');
    }
    const token = authHeader.substring(7);
    try {
      const decoded: any = this.jwtService.verify(token);
      return this.authService.getProfile(decoded.sub);
    } catch (err: any) {
      if (err instanceof UnauthorizedException) throw err;
      throw new UnauthorizedException('Invalid or expired authentication session');
    }
  }
}
