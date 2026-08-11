import { Controller, Get, Post, Body, UseGuards } from '@nestjs/common';
import { ApiTags, ApiOperation, ApiBearerAuth } from '@nestjs/swagger';
import { AuthGuard } from '@nestjs/passport';
import { UsersService } from './users.service';
import { BanUserDto, CustomDiscountDto } from './dto/user-admin.dto';
import { RolesGuard, Roles } from '../../guards/roles.guard';

@ApiTags('Admin Users')
@Controller('api/v1/admin/users')
export class UsersController {
  constructor(private readonly usersService: UsersService) {}

  @Get()
  @ApiOperation({ summary: 'Admin: Get list of all registered users and their subscriptions' })
  getAllUsers() {
    return this.usersService.getAllUsers();
  }

  @Post('ban')
  @ApiOperation({ summary: 'Admin: Ban or unban user account' })
  banUser(@Body() dto: BanUserDto) {
    return this.usersService.banUser(dto);
  }

  @Post('discount')
  @ApiOperation({ summary: 'Admin: Grant custom percentage discount to user' })
  setUserDiscount(@Body() dto: CustomDiscountDto) {
    return this.usersService.setUserDiscount(dto);
  }
}
