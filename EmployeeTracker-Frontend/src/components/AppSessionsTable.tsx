import { useState, useEffect } from "react";
import { AppSession, getDeviceAppSessions } from "@/api/devices";
import { LoadingState } from "@/components/LoadingSpinner";
import { ErrorMessage } from "@/components/ErrorMessage";
import { EmptyState } from "@/components/EmptyState";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Search } from "lucide-react";
import { formatTimeOfDay, formatSecondsToTime, calculateDurationSeconds } from "@/utils/format";

interface AppSessionsTableProps {
  deviceId: string;
  date: string;
}

export function AppSessionsTable({ deviceId, date }: AppSessionsTableProps) {
  const [sessions, setSessions] = useState<AppSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const fetchSessions = async () => {
      setLoading(true);
      setError(null);
      try {
        // Convert date to start/end timestamps
        const startDate = new Date(date + "T00:00:00Z");
        const endDate = new Date(date + "T23:59:59Z");
        
        const data = await getDeviceAppSessions(
          deviceId,
          page,
          100, // Load more per page
          startDate.toISOString(),
          endDate.toISOString(),
          search || undefined
        );
        setSessions(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load app sessions");
      } finally {
        setLoading(false);
      }
    };

    fetchSessions();
  }, [deviceId, date, page, search]);

  if (loading) {
    return <LoadingState message="Loading app sessions..." />;
  }

  if (error) {
    return <ErrorMessage message={error} />;
  }

  return (
    <div className="space-y-4">
      {/* Search */}
      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search by app name or window title..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-10"
          />
        </div>
      </div>

      {/* Table */}
      {sessions.length === 0 ? (
        <EmptyState
          title="No app sessions"
          description={search ? "No sessions match your search" : "No application activity recorded for this date"}
        />
      ) : (
        <div className="rounded-md border">
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-muted/50">
                <tr className="border-b">
                  <th className="text-left p-3 font-medium">Application</th>
                  <th className="text-left p-3 font-medium">Window Title</th>
                  <th className="text-left p-3 font-medium">Start Time</th>
                  <th className="text-right p-3 font-medium">Duration</th>
                </tr>
              </thead>
              <tbody>
                {sessions.map((session, index) => {
                  const duration = calculateDurationSeconds(session.startAt, session.endAt);
                  return (
                    <tr key={session.sessionId} className={index % 2 === 0 ? "bg-background" : "bg-muted/20"}>
                      <td className="p-3 font-mono text-sm">{session.processName}</td>
                      <td className="p-3 text-sm text-muted-foreground">
                        {session.windowTitle || <span className="italic">(none)</span>}
                      </td>
                      <td className="p-3 text-sm">{formatTimeOfDay(session.startAt)}</td>
                      <td className="p-3 text-right font-mono text-sm">{formatSecondsToTime(duration)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Stats */}
      <div className="text-sm text-muted-foreground">
        Showing {sessions.length} session{sessions.length !== 1 ? 's' : ''}
      </div>
    </div>
  );
}
