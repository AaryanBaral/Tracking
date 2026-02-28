import { useState, useEffect, useCallback, useLayoutEffect, useRef } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { ArrowLeft, Clock, Globe, Monitor, Calendar, Power } from "lucide-react";
import gsap from "gsap";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PageHeader } from "@/components/PageHeader";
import { LoadingState } from "@/components/LoadingSpinner";
import { ErrorMessage } from "@/components/ErrorMessage";
import { EmptyState } from "@/components/EmptyState";
import { DeviceSummary, getDeviceSummary } from "@/api/devices";
import { ApiError } from "@/api/client";
import { clearAuthToken } from "@/lib/auth";
import { formatSecondsToTime, formatDateString, getTodayDateString, formatDateTime } from "@/utils/format";
import OrbitBackdrop from "@/components/OrbitBackdrop";
import UserActions from "@/components/UserActions";

export default function DeviceSummaryPage() {
  const { deviceId } = useParams<{ deviceId: string }>();
  const [summary, setSummary] = useState<DeviceSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedDate, setSelectedDate] = useState(getTodayDateString());
  const navigate = useNavigate();
  const containerRef = useRef<HTMLDivElement | null>(null);

  const fetchSummary = useCallback(async () => {
    if (!deviceId) return;
    
    setLoading(true);
    setError(null);
    try {
      const data = await getDeviceSummary(deviceId, selectedDate);
      console.log(data);
      setSummary(data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        clearAuthToken();
        navigate("/login", { replace: true });
        return;
      }
      const message = err instanceof Error ? err.message : "Failed to fetch summary";
      setError(message);
      setSummary(null);
    } finally {
      setLoading(false);
    }
  }, [deviceId, navigate, selectedDate]);

  useEffect(() => {
    fetchSummary();
  }, [fetchSummary]);

  useLayoutEffect(() => {
    if (!containerRef.current || loading) return;
    const ctx = gsap.context(() => {
      gsap.from("[data-animate='header']", {
        opacity: 0,
        y: 20,
        duration: 0.6,
        ease: "power2.out",
      });
      gsap.from("[data-animate='card']", {
        opacity: 0,
        y: 30,
        duration: 0.6,
        ease: "power2.out",
        stagger: 0.12,
        delay: 0.1,
      });
    }, containerRef);
    return () => ctx.revert();
  }, [loading, summary]);

  const handleDateChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSelectedDate(e.target.value);
  };

  if (!deviceId) {
    return (
      <div className="p-6 max-w-4xl mx-auto">
        <ErrorMessage message="Device ID is required" />
      </div>
    );
  }

  const deviceName = summary?.device?.displayName || "(Unassigned)";
  const hostname = summary?.device?.hostname || "Unknown";
  const domainItems = summary?.topDomains
    ? [...summary.topDomains].sort((a, b) => b.seconds - a.seconds)
    : [];
  const urlItems = summary?.topUrls
    ? [...summary.topUrls].sort((a, b) => b.seconds - a.seconds)
    : [];
  const appItems = summary?.topApps
    ? [...summary.topApps].sort((a, b) => b.seconds - a.seconds)
    : [];

  return (
    <div ref={containerRef} className="relative overflow-hidden">
      <OrbitBackdrop className="pointer-events-none absolute -top-24 right-0 h-[320px] w-[320px] opacity-70" />
      <div className="relative z-10 p-6 md:p-10 max-w-5xl mx-auto">
        <div className="mb-4" data-animate="header">
          <Button variant="ghost" size="sm" asChild>
            <Link to="/devices">
              <ArrowLeft className="h-4 w-4 mr-2" />
              Back to Devices
            </Link>
          </Button>
        </div>

        <div data-animate="header">
          <PageHeader
            title={loading && !summary ? "Loading..." : deviceName}
            description={
              loading && !summary
                ? undefined
                : `Hostname: ${hostname} • Device ID: ${deviceId}`
            }
            actions={
              <>
                <div className="flex items-center gap-2">
                  <Calendar className="h-4 w-4 text-muted-foreground" />
                  <Input
                    type="date"
                    value={selectedDate}
                    onChange={handleDateChange}
                    max={getTodayDateString()}
                    className="w-auto bg-white/80"
                  />
                </div>
                <UserActions />
              </>
            }
          />
        </div>

      {loading ? (
        <LoadingState message="Loading summary..." />
      ) : error ? (
        <ErrorMessage message={error} onRetry={fetchSummary} />
      ) : !summary ? (
        <EmptyState
          title="No data available"
          description={`No activity data found for ${formatDateString(selectedDate)}`}
        />
      ) : (
        <div className="space-y-8">
          <div className="text-sm text-muted-foreground" data-animate="header">
            Showing data for:{" "}
            <span className="font-medium text-foreground">{formatDateString(summary.date)}</span>
          </div>

          <div className="grid md:grid-cols-2 gap-6" data-animate="card">
            <Card className="glass-panel">
              <CardHeader className="pb-2">
                <CardTitle className="text-base font-semibold flex items-center gap-2">
                  <Power className="h-4 w-4 text-primary" />
                  Device On Time
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-mono font-semibold">
                  {formatSecondsToTime(summary.deviceOnSeconds)}
                </div>
                <p className="text-sm text-muted-foreground mt-1">
                  Total time device was on
                </p>
                <div className="text-xs text-muted-foreground mt-3 space-y-1">
                  <div>Start: {formatDateTime(summary.deviceOnStartAtUtc)}</div>
                  <div>End: {formatDateTime(summary.deviceOnEndAtUtc)}</div>
                </div>
              </CardContent>
            </Card>

            <Card className="glass-panel">
              <CardHeader className="pb-2">
                <CardTitle className="text-base font-semibold flex items-center gap-2">
                  <Clock className="h-4 w-4 text-warning" />
                  Idle Time
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-mono font-semibold">
                  {formatSecondsToTime(summary.idleSeconds)}
                </div>
                <p className="text-sm text-muted-foreground mt-1">
                  Total idle time recorded
                </p>
              </CardContent>
            </Card>
          </div>

          <div className="grid gap-6" data-animate="card">
            <Card className="glass-panel">
              <CardHeader className="pb-2">
                <CardTitle className="text-base font-semibold flex items-center gap-2">
                  <Globe className="h-4 w-4 text-primary" />
                  All Domains
                </CardTitle>
              </CardHeader>
              <CardContent className="max-h-[320px] overflow-y-auto">
                {domainItems.length === 0 ? (
                  <p className="text-sm text-muted-foreground py-4 text-center">
                    No domain activity recorded
                  </p>
                ) : (
                  <ul className="space-y-2">
                    {domainItems.map((item, index) => (
                      <li
                        key={`${item.name}-${index}`}
                        className="flex items-center justify-between gap-4 py-2 border-b last:border-0"
                      >
                        <span className="text-sm truncate flex-1">
                          {item.name || "(unknown)"}
                        </span>
                        <span className="text-sm font-mono text-muted-foreground whitespace-nowrap">
                          {formatSecondsToTime(item.seconds)}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>

            <Card className="glass-panel">
              <CardHeader className="pb-2">
                <CardTitle className="text-base font-semibold flex items-center gap-2">
                  <Globe className="h-4 w-4 text-primary" />
                  All URLs
                </CardTitle>
              </CardHeader>
              <CardContent className="max-h-[320px] overflow-y-auto">
                {urlItems.length === 0 ? (
                  <p className="text-sm text-muted-foreground py-4 text-center">
                    No URL activity recorded
                  </p>
                ) : (
                  <ul className="space-y-3">
                    {urlItems.map((item, index) => (
                      <li
                        key={`${item.name}-${index}`}
                        className="grid grid-cols-[minmax(0,1fr)_88px] items-start gap-3 py-2 border-b last:border-0"
                      >
                        <span className="text-xs md:text-sm text-foreground break-words">
                          {item.name || "(unknown)"}
                        </span>
                        <span className="text-sm font-mono text-muted-foreground whitespace-nowrap text-right">
                          {formatSecondsToTime(item.seconds)}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>
          </div>

          <Card className="glass-panel" data-animate="card">
            <CardHeader className="pb-2">
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <Monitor className="h-4 w-4 text-success" />
                All Applications
              </CardTitle>
            </CardHeader>
            <CardContent className="max-h-[320px] overflow-y-auto">
              {appItems.length === 0 ? (
                <p className="text-sm text-muted-foreground py-4 text-center">
                  No application activity recorded
                </p>
              ) : (
                <ul className="space-y-2">
                  {appItems.map((item, index) => (
                    <li
                      key={`${item.name}-${index}`}
                      className="flex items-center justify-between gap-4 py-2 border-b last:border-0"
                    >
                      <span className="text-sm font-mono truncate flex-1">
                        {item.name || "(unknown)"}
                      </span>
                      <span className="text-sm font-mono text-muted-foreground whitespace-nowrap">
                        {formatSecondsToTime(item.seconds)}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      )}
      </div>
    </div>
  );
}
