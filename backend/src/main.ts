import { NestFactory } from '@nestjs/core';
import { ValidationPipe } from '@nestjs/common';
import { SwaggerModule, DocumentBuilder } from '@nestjs/swagger';
import { AppModule } from './app.module';

async function bootstrap() {
  const app = await NestFactory.create(AppModule);

  // Enable CORS
  app.enableCors({
    origin: '*',
    methods: 'GET,HEAD,PUT,PATCH,POST,DELETE,OPTIONS',
    credentials: true,
  });

  // Enable ValidationPipe globally for request DTO validation
  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      transform: true,
      forbidNonWhitelisted: false,
    }),
  );

  // Configure Swagger OpenAPI Docs
  const config = new DocumentBuilder()
    .setTitle('RouteXia Enterprise Management & Admin API')
    .setDescription(
      'REST API documentation for RouteXia Multipath Game Accelerator administration, authentication, relay server management, user subscriptions, and update distribution.',
    )
    .setVersion('2.0.0')
    .addBearerAuth()
    .build();

  const document = SwaggerModule.createDocument(app, config);
  SwaggerModule.setup('api/docs', app, document, {
    customSiteTitle: 'RouteXia API Documentation',
  });

  const port = process.env.PORT || 8080;
  await app.listen(port);

  console.log(`=======================================================`);
  console.log(`🚀 RouteXia NestJS Admin & Management Backend Started `);
  console.log(`🌐 Server running at: http://localhost:${port}`);
  console.log(`📚 Swagger API Docs:  http://localhost:${port}/api/docs`);
  console.log(`📊 Admin Portal:       http://localhost:${port}/admin/`);
  console.log(`=======================================================`);
}
bootstrap();
