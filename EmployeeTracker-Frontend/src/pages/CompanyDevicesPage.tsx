import { useState, useEffect, useCallback, useLayoutEffect, useRef } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, RefreshCw, Eye } from "lucide-react";
import gsap from "gsap";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { PageHeader } from "@/components/PageHeader";
import { LoadingState } from "@/components/LoadingSpinner";
import { ErrorMessage } from "@/components/ErrorMessage";
import { EmptyState } from "@/components/EmptyState";
import { ApiError } from "@/api/client";
import { getCompanyDevices } from "@/api/companies";
import { Device } from "@/api/devices";
import { clearAuthToken } from "@/lib/auth";
import { formatRelativeTime } from "@/utils/format";
import OrbitBackdrop from "@/components/OrbitBackdrop";
import UserActions from "@/components/UserActions";

export default function CompanyDevicesPage() {
  const { companyId } = useParams<{ companyId: string }>();
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();
  const containerRef = useRef<HTMLDivElement | null>(null);

  const fetchDevices = useCallback(async () => {
    if (!companyId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await getCompanyDevices(companyId);
      const sorted = [...data].sort((a, b) => {
        const dateA = a.lastSeenAt ? new Date(a.lastSeenAt).getTime() : 0;
        const dateB = b.lastSeenAt ? new Date(b.lastSeenAt).getTime() : 0;
        return dateB - dateA;
      });
      setDevices(sorted);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        clearAuthToken();
        navigate("/login", { replace: true });
        return;
      }
      const message = err instanceof Error ? err.message : "Failed to fetch devices";
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [companyId, navigate]);

  useEffect(() => {
    fetchDevices();
  }, [fetchDevices]);

  useLayoutEffect(() => {
    if (!containerRef.current || loading) return;
    const ctx = gsap.context(() => {
      gsap.from("[data-animate='header']", {
        opacity: 0,
        y: 16,
        duration: 0.6,
        ease: "power2.out",
      });
      gsap.from("[data-animate='panel']", {
        opacity: 0,
        y: 24,
        duration: 0.6,
        ease: "power2.out",
        delay: 0.1,
      });
    }, containerRef);
    return () => ctx.revert();
  }, [loading, devices.length]);

  if (!companyId) {
    return (
      <div className="p-6 max-w-4xl mx-auto">
        <ErrorMessage message="Company ID is required" />
      </div>
    );
  }

  return (
    <div ref={containerRef} className="relative z-10 p-6 md:p-10 max-w-6xl mx-auto">
      <OrbitBackdrop className="pointer-events-none absolute -top-24 right-10 h-[240px] w-[240px] opacity-45" />
      <div className="mb-4" data-animate="header">
        <Button variant="ghost" size="sm" asChild>
          <Link to="/companies">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Companies
          </Link>
        </Button>
      </div>

      <div data-animate="header">
        <PageHeader
        title="Company Devices"
        description={`Devices enrolled under ${companyId}`}
        actions={
          <>
            <Button onClick={fetchDevices} variant="outline" size="sm" disabled={loading}>
              <RefreshCw className={`h-4 w-4 mr-2 ${loading ? "animate-spin" : ""}`} />
              Refresh
            </Button>
            <UserActions />
          </>
        }
        />
      </div>

      {loading && devices.length === 0 ? (
        <LoadingState message="Loading devices..." />
      ) : error ? (
        <ErrorMessage message={error} onRetry={fetchDevices} />
      ) : devices.length === 0 ? (
        <EmptyState
          title="No devices found"
          description="No devices have been enrolled under this company yet."
        />
      ) : (
        <div className="glass-panel overflow-hidden" data-animate="panel">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/50">
                <TableHead className="w-[200px]">Name</TableHead>
                <TableHead>Hostname</TableHead>
                <TableHead className="w-[280px]">Device ID</TableHead>
                <TableHead className="w-[140px]">Last Seen</TableHead>
                <TableHead className="w-[120px] text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {devices.map((device) => (
                <TableRow key={device.deviceId}>
                  <TableCell>
                    <span className={device.displayName ? "font-medium" : "text-muted-foreground italic"}>
                      {device.displayName || "(Unassigned)"}
                    </span>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {device.hostname || "—"}
                  </TableCell>
                  <TableCell>
                    <code className="text-xs font-mono bg-muted px-2 py-1 rounded">
                      {device.deviceId}
                    </code>
                  </TableCell>
                  <TableCell className="text-muted-foreground text-sm">
                    {formatRelativeTime(device.lastSeenAt)}
                  </TableCell>
                  <TableCell className="text-right">
                    <Button variant="ghost" size="sm" asChild>
                      <Link to={`/devices/${device.deviceId}`}>
                        <Eye className="h-4 w-4" />
                      </Link>
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
