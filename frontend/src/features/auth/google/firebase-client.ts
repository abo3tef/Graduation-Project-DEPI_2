import {getApp, getApps, initializeApp, type FirebaseApp} from 'firebase/app';
import {getAuth, GoogleAuthProvider, signInWithPopup, type Auth} from 'firebase/auth';

/**
 * Checks if the public Firebase configuration keys are present in the environment.
 * The button renders even when unconfigured, reporting a clear message on click.
 */
export function isFirebaseConfigured(): boolean {
  const apiKey = process.env.NEXT_PUBLIC_FIREBASE_API_KEY?.trim();
  const projectId = process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID?.trim();

  return Boolean(apiKey && projectId);
}

function getFirebaseApp(): FirebaseApp {
  if (getApps().length > 0) {
    return getApp();
  }

  return initializeApp({
    apiKey: process.env.NEXT_PUBLIC_FIREBASE_API_KEY,
    authDomain: process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN,
    projectId: process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID,
    storageBucket: process.env.NEXT_PUBLIC_FIREBASE_STORAGE_BUCKET,
    messagingSenderId: process.env.NEXT_PUBLIC_FIREBASE_MESSAGING_SENDER_ID,
    appId: process.env.NEXT_PUBLIC_FIREBASE_APP_ID
  });
}

export function getFirebaseAuth(): Auth {
  return getAuth(getFirebaseApp());
}

/**
 * Performs Google sign-in using Firebase Auth popup and retrieves the raw ID token.
 */
export async function requestGoogleIdToken(): Promise<string> {
  if (!isFirebaseConfigured()) {
    throw new Error('FIREBASE_NOT_CONFIGURED');
  }

  const auth = getFirebaseAuth();
  const provider = new GoogleAuthProvider();
  provider.setCustomParameters({prompt: 'select_account'});

  const credential = await signInWithPopup(auth, provider);
  return await credential.user.getIdToken();
}

/**
 * Tests whether an error caught from Firebase indicates the user dismissed the sign-in popup.
 */
export function isFirebaseCancelled(error: unknown): boolean {
  if (typeof error === 'object' && error !== null && 'code' in error) {
    const code = (error as {code: unknown}).code;
    return code === 'auth/popup-closed-by-user' || code === 'auth/cancelled-popup-request';
  }
  return false;
}
