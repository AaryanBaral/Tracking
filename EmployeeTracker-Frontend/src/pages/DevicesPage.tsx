import { useState, useEffect, useCallback, useLayoutEffect, useRef } from "react";
import { Link, useNavigate } from "react-router-dom";
import { RefreshCw, Pencil, Check, X, Eye } from "lucide-react";
import gsap from "gsap";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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
import { Device, getDevices, updateDeviceDisplayName } from "@/api/devices";
import { ApiError } from "@/api/client";
import { clearAuthToken, getAuthRole } from "@/lib/auth";
import { formatRelativeTime } from "@/utils/format";
import { useToast } from "@/hooks/use-toast";
import OrbitBackdrop from "@/components/OrbitBackdrop";
import UserActions from "@/components/UserActions";

export default function DevicesPage() {
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editValue, setEditValue] = useState("");
  const [saving, setSaving] = useState(false);
  const { toast } = useToast();
  const role = getAuthRole();
  const navigate = useNavigate();
  const containerRef = useRef<HTMLDivElement | null>(null);

  const fetchDevices = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getDevices();
      // Sort by lastSeenAt descending
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
  }, [navigate]);

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

  const handleEdit = (device: Device) => {
    setEditingId(device.deviceId);
    setEditValue(device.displayName || "");
  };

  const handleCancel = () => {
    setEditingId(null);
    setEditValue("");
  };

  const handleSave = async (deviceId: string) => {
    setSaving(true);
    try {
      const newName = editValue.trim() || null;
      await updateDeviceDisplayName(deviceId, newName);
      
      // Update local state
      setDevices((prev) =>
        prev.map((d) =>
          d.deviceId === deviceId ? { ...d, displayName: newName } : d
        )
      );
      
      toast({
        title: "Device updated",
        description: newName ? `Name set to "${newName}"` : "Name cleared",
        variant: "success",
      });
      
      setEditingId(null);
      setEditValue("");
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        clearAuthToken();
        navigate("/login", { replace: true });
        return;
      }
      const message = err instanceof Error ? err.message : "Failed to update device";
      toast({
        title: "Update failed",
        description: message,
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent, deviceId: string) => {
    if (e.key === "Enter") {
      handleSave(deviceId);
    } else if (e.key === "Escape") {
      handleCancel();
    }
  };

  return (
    <div ref={containerRef} className="relative z-10 p-6 md:p-10 max-w-6xl mx-auto">
      <OrbitBackdrop className="pointer-events-none absolute -top-24 right-10 h-[260px] w-[260px] opacity-50" />
      <div data-animate="header">
        <PageHeader
        title="Devices"
        description="Manage and monitor all tracked devices"
        actions={
          <>
            {role === "SystemAdmin" && (
              <Button variant="outline" size="sm" asChild>
                <Link to="/companies">Companies</Link>
              </Button>
            )}
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
          description="No devices have been registered yet. Devices will appear here once they start reporting."
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
                <TableHead className="w-[180px] text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {devices.map((device) => (
                <TableRow key={device.deviceId}>
                  <TableCell>
                    {editingId === device.deviceId ? (
                      <Input
                        value={editValue}
                        onChange={(e) => setEditValue(e.target.value)}
                        onKeyDown={(e) => handleKeyDown(e, device.deviceId)}
                        placeholder="Enter name..."
                        className="h-8"
                        autoFocus
                        disabled={saving}
                      />
                    ) : (
                      <span className={device.displayName ? "font-medium" : "text-muted-foreground italic"}>
                        {device.displayName || "(Unassigned)"}
                      </span>
                    )}
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
                    {editingId === device.deviceId ? (
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleSave(device.deviceId)}
                          disabled={saving}
                        >
                          <Check className="h-4 w-4 text-success" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={handleCancel}
                          disabled={saving}
                        >
                          <X className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    ) : (
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleEdit(device)}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button variant="ghost" size="sm" asChild>
                          <Link to={`/devices/${device.deviceId}`}>
                            <Eye className="h-4 w-4" />
                          </Link>
                        </Button>
                      </div>
                    )}
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
