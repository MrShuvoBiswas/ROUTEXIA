import { Injectable, OnModuleInit, Logger, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as firebaseAdmin from 'firebase-admin';
import { App, getApps, initializeApp, cert } from 'firebase-admin/app';
import { getAuth, DecodedIdToken } from 'firebase-admin/auth';

/**
 * FirebaseAdminService
 *
 * Initialises the Firebase Admin SDK using credentials loaded from
 * environment variables — no secrets are hard-coded.
 *
 * Required env vars (set in .env):
 *   FIREBASE_PROJECT_ID       = your Firebase project ID
 *   FIREBASE_CLIENT_EMAIL     = service-account email
 *   FIREBASE_PRIVATE_KEY      = service-account private key (with \n escaped)
 *
 * OR alternatively:
 *   FIREBASE_SERVICE_ACCOUNT_PATH = absolute path to service-account JSON file
 */
@Injectable()
export class FirebaseAdminService implements OnModuleInit {
  private readonly logger = new Logger(FirebaseAdminService.name);
  private app: App | null = null;

  constructor(private readonly config: ConfigService) {}

  onModuleInit() {
    if (getApps().length > 0) {
      this.app = getApps()[0];
      return;
    }

    const serviceAccountPath = this.config.get<string>('FIREBASE_SERVICE_ACCOUNT_PATH');

    if (serviceAccountPath) {
      // Option A: JSON file path
      // eslint-disable-next-line @typescript-eslint/no-var-requires
      const serviceAccount = require(serviceAccountPath);
      this.app = initializeApp({ credential: cert(serviceAccount) });
      this.logger.log(`Firebase Admin initialised from file: ${serviceAccountPath}`);
      return;
    }

    // Option B: Individual env vars
    const projectId   = this.config.get<string>('FIREBASE_PROJECT_ID');
    const clientEmail = this.config.get<string>('FIREBASE_CLIENT_EMAIL');
    const privateKey  = this.config.get<string>('FIREBASE_PRIVATE_KEY');

    if (!projectId || !clientEmail || !privateKey) {
      this.logger.warn(
        'Firebase Admin SDK is NOT initialised. ' +
        'Set FIREBASE_PROJECT_ID, FIREBASE_CLIENT_EMAIL, FIREBASE_PRIVATE_KEY in .env.',
      );
      return;
    }

    this.app = initializeApp({
      credential: cert({
        projectId,
        clientEmail,
        // \n stored as \\n in env files — restore real newlines
        privateKey: privateKey.replace(/\\n/g, '\n'),
      }),
    });

    this.logger.log(`Firebase Admin initialised for project: ${projectId}`);
  }

  /**
   * Verifies a Firebase ID token (sent from the WPF client after sign-in).
   * Returns the decoded token with uid, email, etc.
   * Throws UnauthorizedException on failure.
   */
  async verifyIdToken(idToken: string): Promise<DecodedIdToken> {
    if (!this.app) {
      throw new UnauthorizedException(
        'Firebase Admin SDK is not configured on this server.',
      );
    }

    try {
      const decoded = await getAuth(this.app).verifyIdToken(idToken, true);
      return decoded;
    } catch (err: any) {
      this.logger.warn(`Firebase token verification failed: ${err?.message}`);
      throw new UnauthorizedException('Invalid or expired Firebase ID token.');
    }
  }

  /**
   * Generates a custom-branded password reset link using Firebase Admin SDK.
   * Extracts the oobCode to construct our custom RouteXia URL:
   * https://app.routexia.in/auth/action?mode=resetPassword&oobCode=...
   */
  async generatePasswordResetLink(email: string): Promise<string> {
    if (!this.app) {
      throw new UnauthorizedException(
        'Firebase Admin SDK is not configured on this server.',
      );
    }

    try {
      const rawLink = await getAuth(this.app).generatePasswordResetLink(email);
      const url = new URL(rawLink);
      const oobCode = url.searchParams.get('oobCode');
      const apiKey = url.searchParams.get('apiKey') || 'AIzaSyBJtxmLbeeKe-XIcsKRhDkoPBTkmXPcPcQ';
      
      const customLink = `https://app.routexia.in/auth/action?mode=resetPassword&oobCode=${oobCode}&apiKey=${apiKey}`;
      this.logger.log(`Generated custom password reset link for ${email}`);
      return customLink;
    } catch (err: any) {
      this.logger.error(`Failed to generate password reset link for ${email}: ${err?.message}`);
      throw new UnauthorizedException(err?.message || 'Failed to generate password reset link.');
    }
  }
}
