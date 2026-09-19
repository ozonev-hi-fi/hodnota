const PLATFORM_LABELS: Record<string, string> = {
  spotify: 'Spotify',
  youtube: 'YouTube',
  'youtube-music': 'YouTube Music',
  qobuz: 'Qobuz',
  tidal: 'Tidal',
  deezer: 'Deezer',
  'apple-music': 'Apple Music',
  bandcamp: 'Bandcamp',
};

export function platformLabel(code: string): string {
  return PLATFORM_LABELS[code] ?? code;
}
