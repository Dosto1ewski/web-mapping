const CROCKFORD = '0123456789ABCDEFGHJKMNPQRSTVWXYZ';
const ENC = new TextEncoder();

function toBase64url(bytes: Uint8Array): string {
  let bin = '';
  for (const b of bytes) bin += String.fromCharCode(b);
  return btoa(bin).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
}

function fromBase64url(s: string): Uint8Array {
  const padded = s.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(s.length / 4) * 4, '=');
  const bin = atob(padded);
  return Uint8Array.from(bin, (c) => c.charCodeAt(0));
}

export function generateInviteCode(): string {
  // 10 random bytes → 80 bits → 16 Crockford Base32 chars (5 bits each)
  const bytes = new Uint8Array(10);
  crypto.getRandomValues(bytes);
  let bits = 0n;
  for (const b of bytes) bits = (bits << 8n) | BigInt(b);
  let code = '';
  for (let i = 0; i < 16; i++) {
    code = CROCKFORD[Number(bits & 0x1fn)] + code;
    bits >>= 5n;
  }
  return `${code.slice(0, 8)}-${code.slice(8)}`;
}

export async function hashInviteCode(code: string): Promise<string> {
  const hash = await crypto.subtle.digest('SHA-256', ENC.encode(code));
  return Array.from(new Uint8Array(hash))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('');
}

export async function deriveGroupKey(inviteCode: string, groupId: string): Promise<CryptoKey> {
  const ikm = await crypto.subtle.importKey('raw', ENC.encode(inviteCode), 'HKDF', false, ['deriveKey']);
  return crypto.subtle.deriveKey(
    { name: 'HKDF', hash: 'SHA-256', salt: ENC.encode(groupId), info: ENC.encode('standort-v1-location') },
    ikm,
    { name: 'AES-GCM', length: 256 },
    false,
    ['encrypt', 'decrypt'],
  );
}

export async function encrypt(key: CryptoKey, plaintext: string): Promise<string> {
  const iv = new Uint8Array(12);
  crypto.getRandomValues(iv);
  const ct = await crypto.subtle.encrypt({ name: 'AES-GCM', iv }, key, ENC.encode(plaintext));
  return `e1.${toBase64url(iv)}.${toBase64url(new Uint8Array(ct))}`;
}

export async function decrypt(key: CryptoKey, blob: string): Promise<string> {
  const parts = blob.split('.');
  if (parts.length !== 3 || parts[0] !== 'e1') throw new Error('Invalid encrypted blob');
  const iv = new Uint8Array(fromBase64url(parts[1]));
  const ct = new Uint8Array(fromBase64url(parts[2]));
  const plain = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, key, ct);
  return new TextDecoder().decode(plain);
}

export function isEncrypted(v: unknown): v is string {
  return typeof v === 'string' && v.startsWith('e1.');
}
