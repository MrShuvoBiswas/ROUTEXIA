# RouteXia — NestJS Management & Admin Backend Guide (v2.0)

This guide documents the enterprise **Management & Subscription Admin Backend** built with **NestJS (TypeScript)** and **Neon Serverless PostgreSQL**.

---

## 🌟 Key Features

1. **Neon Serverless PostgreSQL**: High-performance, autoscaling cloud database with SSL encryption.
2. **Interactive Swagger OpenAPI Docs**: Automatically generated API docs at `/api/docs`.
3. **1-Click Docker Deployment**: Dockerized container setup via `docker-compose.yml`.
4. **Anti-Abuse HWID Trial Engine**: Ensures physical PCs get only 1 free trial account.
5. **Modern Dark-Theme Admin Portal**: Dashboard served directly under `/admin/`.

---

## 🛠️ Architecture Components

```
backend/
├── src/
│   ├── main.ts                    # NestJS bootstrap with Swagger & CORS
│   ├── app.module.ts              # Root module & Neon DB TypeORM configuration
│   ├── entities/                  # TypeORM PostgreSQL entities (User, Device, Subscription, Relay, Coupon, AppVersion)
│   ├── guards/                    # JWT & Role-Based Access Control (Admin/User)
│   └── modules/
│       ├── auth/                  # JWT Register/Login & HWID Anti-Abuse
│       ├── users/                 # Admin User Management & Custom Discounts
│       ├── subscriptions/         # Subscription extensions & plans
│       ├── relays/                # Dynamic Relay inventory management (SG, IN, DXB)
│       ├── coupons/               # Promotional discount codes
│       ├── versions/              # Client Launcher OTA update manager
│       └── admin/                 # Aggregated stats dashboard
├── public/                        # Admin Portal Web UI (index.html)
├── Dockerfile                     # Multi-stage production container build
├── docker-compose.yml             # 1-Click VPS orchestration
└── .env                           # Environment secrets & Neon DB connection
```

---

## 🚀 How to Run & Deploy

### Option 1: Local Development

1. **Install Dependencies**:
   ```bash
   cd backend
   npm install
   ```

2. **Configure Environment Variables**:
   Copy `.env.example` to `.env` and set your Neon DB connection string:
   ```env
   PORT=8080
   DATABASE_URL=postgres://neondb_owner:YOUR_NEON_PASSWORD@ep-routexia-db.ap-southeast-1.aws.neon.tech/routexia?sslmode=require
   JWT_SECRET=RouteXia_Super_Secret_JWT_Key_2026_Enterprise!
   ADMIN_EMAIL=admin@routexia.com
   ADMIN_PASSWORD=Admin123456!
   ```

3. **Start Development Server**:
   ```bash
   npm run start:dev
   ```

4. **Access Endpoints**:
   - **Admin Control Center**: [http://localhost:8080/admin/](http://localhost:8080/admin/)
   - **Swagger API Docs**: [http://localhost:8080/api/docs](http://localhost:8080/api/docs)
   - **Health Check**: [http://localhost:8080/api/v1/health](http://localhost:8080/api/v1/health)

---

### Option 2: Production VPS Deployment (Docker Compose)

Deploy to any Linux VPS (Ubuntu / Debian / CentOS) with Docker installed:

```bash
cd backend
docker-compose up -d --build
```

The server will automatically:
- Connect to Neon DB PostgreSQL with SSL
- Auto-apply database schema & migrations
- Seed the default Admin user (`admin@routexia.com`)
- Seed default Relay Nodes (Singapore, India Mumbai, Dubai)
- Start listening on port `8080`

---

## 🔑 Initial Admin Credentials

- **Email**: `admin@routexia.com`
- **Password**: `Admin123456!`
