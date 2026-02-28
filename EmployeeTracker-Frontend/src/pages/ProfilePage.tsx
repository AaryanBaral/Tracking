import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Building2, ShieldCheck, User as UserIcon } from "lucide-react";
import gsap from "gsap";
import { PageHeader } from "@/components/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { LoadingState } from "@/components/LoadingSpinner";
import { ErrorMessage } from "@/components/ErrorMessage";
import { ApiError } from "@/api/client";
import { getProfile, ProfileResponse } from "@/api/auth";
import { clearAuthToken } from "@/lib/auth";
import OrbitBackdrop from "@/components/OrbitBackdrop";
import UserActions from "@/components/UserActions";

export default function ProfilePage() {
  const [profile, setProfile] = useState<ProfileResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();
  const containerRef = useRef<HTMLDivElement | null>(null);

  const fetchProfile = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getProfile();
      setProfile(data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        clearAuthToken();
        navigate("/login", { replace: true });
        return;
      }
      const message = err instanceof Error ? err.message : "Failed to load profile";
      setError(message);
      setProfile(null);
    } finally {
      setLoading(false);
    }
  }, [navigate]);

  useEffect(() => {
    fetchProfile();
  }, [fetchProfile]);

  useLayoutEffect(() => {
    if (!containerRef.current || loading) return;
    const ctx = gsap.context(() => {
      gsap.from("[data-animate='header']", {
        opacity: 0,
        y: 16,
        duration: 0.6,
        ease: "power2.out",
      });
      gsap.from("[data-animate='card']", {
        opacity: 0,
        y: 24,
        duration: 0.6,
        ease: "power2.out",
        stagger: 0.12,
        delay: 0.1,
      });
    }, containerRef);
    return () => ctx.revert();
  }, [loading, profile]);

  return (
    <div ref={containerRef} className="relative z-10 p-6 md:p-10 max-w-5xl mx-auto">
      <OrbitBackdrop className="pointer-events-none absolute -top-24 right-8 h-[280px] w-[280px] opacity-50" />
      <div data-animate="header">
        <PageHeader
          title="Profile"
          description="View your account and company details."
          actions={<UserActions />}
        />
      </div>

      {loading ? (
        <LoadingState message="Loading profile..." />
      ) : error ? (
        <ErrorMessage message={error} onRetry={fetchProfile} />
      ) : !profile ? (
        <ErrorMessage message="Profile data not available." onRetry={fetchProfile} />
      ) : (
        <div className="grid gap-6 md:grid-cols-2" data-animate="card">
          <Card className="glass-panel">
            <CardHeader className="pb-2">
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <UserIcon className="h-4 w-4 text-primary" />
                User
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div className="flex items-center justify-between gap-4">
                <span className="text-muted-foreground">Email</span>
                <span className="font-medium">{profile.email}</span>
              </div>
              <div className="flex items-center justify-between gap-4">
                <span className="text-muted-foreground">Role</span>
                <span className="font-medium">{profile.role}</span>
              </div>
              <div className="flex items-center justify-between gap-4">
                <span className="text-muted-foreground">User ID</span>
                <code className="text-xs font-mono bg-muted px-2 py-1 rounded">
                  {profile.userId}
                </code>
              </div>
            </CardContent>
          </Card>

          <Card className="glass-panel">
            <CardHeader className="pb-2">
              <CardTitle className="text-base font-semibold flex items-center gap-2">
                <Building2 className="h-4 w-4 text-primary" />
                Company
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div className="flex items-center justify-between gap-4">
                <span className="text-muted-foreground">Name</span>
                <span className="font-medium">{profile.companyName || "—"}</span>
              </div>
              <div className="flex items-center justify-between gap-4">
                <span className="text-muted-foreground">Company ID</span>
                <code className="text-xs font-mono bg-muted px-2 py-1 rounded">
                  {profile.companyId}
                </code>
              </div>
              <div className="flex items-center justify-between gap-4">
                <span className="text-muted-foreground">Status</span>
                {profile.companyIsActive === null ? (
                  <span className="text-muted-foreground">—</span>
                ) : (
                  <span
                    className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ${
                      profile.companyIsActive ? "text-success" : "text-destructive"
                    }`}
                  >
                    <ShieldCheck className="h-3.5 w-3.5" />
                    {profile.companyIsActive ? "Active" : "Inactive"}
                  </span>
                )}
              </div>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
