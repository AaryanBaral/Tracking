/**
 * Format seconds to HH:mm:ss display
 */
export function formatSecondsToTime(totalSeconds: number | null | undefined): string {
  if (totalSeconds == null || isNaN(totalSeconds)) {
    return "00:00:00";
  }

  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = Math.floor(totalSeconds % 60);

  return [
    hours.toString().padStart(2, "0"),
    minutes.toString().padStart(2, "0"),
    seconds.toString().padStart(2, "0"),
  ].join(":");
}

/**
 * Format seconds to HH:mm display (without seconds)
 */
export function formatSecondsToHHMM(totalSeconds: number | null | undefined): string {
  if (totalSeconds == null || isNaN(totalSeconds)) {
    return "00:00";
  }

  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);

  return [
    hours.toString().padStart(2, "0"),
    minutes.toString().padStart(2, "0"),
  ].join(":");
}

/**
 * Format ISO date string to human-readable local date/time
 */
export function formatDateTime(isoString: string | null | undefined): string {
  if (!isoString) {
    return "Never";
  }

  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) {
      return "Invalid date";
    }

    return date.toLocaleString(undefined, {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return "Invalid date";
  }
}

/**
 * Format ISO date string to relative time (e.g., "2 hours ago")
 */
export function formatRelativeTime(isoString: string | null | undefined): string {
  if (!isoString) {
    return "Never";
  }

  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) {
      return "Invalid date";
    }

    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffSeconds = Math.floor(diffMs / 1000);
    const diffMinutes = Math.floor(diffSeconds / 60);
    const diffHours = Math.floor(diffMinutes / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffSeconds < 60) {
      return "Just now";
    } else if (diffMinutes < 60) {
      return `${diffMinutes}m ago`;
    } else if (diffHours < 24) {
      return `${diffHours}h ago`;
    } else if (diffDays < 7) {
      return `${diffDays}d ago`;
    } else {
      return formatDateTime(isoString);
    }
  } catch {
    return "Invalid date";
  }
}

/**
 * Get today's date in YYYY-MM-DD format
 */
export function getTodayDateString(): string {
  const today = new Date();
  return today.toISOString().split("T")[0];
}

/**
 * Format a date string (YYYY-MM-DD) to a readable format
 */
export function formatDateString(dateString: string | null | undefined): string {
  if (!dateString) {
    return "Unknown";
  }

  try {
    const date = new Date(dateString + "T00:00:00");
    if (isNaN(date.getTime())) {
      return dateString;
    }

    return date.toLocaleDateString(undefined, {
      weekday: "long",
      year: "numeric",
      month: "long",
      day: "numeric",
    });
  } catch {
  }
}

/**
 * Calculate duration in seconds between two ISO datetime strings
 */
export function calculateDurationSeconds(startAt: string, endAt: string): number {
  try {
    const start = new Date(startAt);
    const end = new Date(endAt);
    if (isNaN(start.getTime()) || isNaN(end.getTime())) {
      return 0;
    }
    return Math.floor((end.getTime() - start.getTime()) / 1000);
  } catch {
    return 0;
  }
}

/**
 * Format time to just HH:MM (for time of day display)
 */
export function formatTimeOfDay(isoString: string | null | undefined): string {
  if (!isoString) return "--:--";
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return "--:--";
    return date.toLocaleTimeString(undefined, {
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return "--:--";
  }
}

