import { useState, useEffect, useCallback, useLayoutEffect, useRef } from "react";
import { Link, useNavigate } from "react-router-dom";
import { RefreshCw, Building2, Eye, Pencil, Check, X, Trash2 } from "lucide-react";
import gsap from "gsap";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
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
import { createCompany, deleteCompany, getCompanies, updateCompany, Company } from "@/api/companies";
import { clearAuthToken } from "@/lib/auth";
import { useToast } from "@/hooks/use-toast";
import OrbitBackdrop from "@/components/OrbitBackdrop";
import UserActions from "@/components/UserActions";

export default function CompaniesPage() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [enrollmentKey, setEnrollmentKey] = useState("");
  const [adminEmail, setAdminEmail] = useState("");
  const [adminPassword, setAdminPassword] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [creating, setCreating] = useState(false);
  const [createdInfo, setCreatedInfo] = useState<{ id: string; name: string; key: string } | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState("");
  const [editKey, setEditKey] = useState("");
  const [editActive, setEditActive] = useState(true);
  const [savingId, setSavingId] = useState<string | null>(null);
  const navigate = useNavigate();
  const { toast } = useToast();
  const containerRef = useRef<HTMLDivElement | null>(null);

  const fetchCompanies = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getCompanies();
      setCompanies(data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        clearAuthToken();
        navigate("/login", { replace: true });
        return;
      }
      const message = err instanceof Error ? err.message : "Failed to fetch companies";
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [navigate]);

  useEffect(() => {
    fetchCompanies();
  }, [fetchCompanies]);

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
  }, [loading, companies.length]);

  const handleCreate = async (event: React.FormEvent) => {
    event.preventDefault();
    const trimmedName = name.trim();
    if (!trimmedName) {
      toast({
        title: "Name required",
        description: "Please enter a company name.",
        variant: "destructive",
      });
      return;
    }
    const trimmedKey = enrollmentKey.trim();
    if (!trimmedKey) {
      toast({
        title: "Enrollment key required",
        description: "Please provide an enrollment key.",
        variant: "destructive",
      });
      return;
    }
    const trimmedEmail = adminEmail.trim();
    if (!trimmedEmail) {
      toast({
        title: "Admin email required",
        description: "Please provide a company admin email.",
        variant: "destructive",
      });
      return;
    }
    const trimmedPassword = adminPassword.trim();
    if (!trimmedPassword) {
      toast({
        title: "Admin password required",
        description: "Please provide a company admin password.",
        variant: "destructive",
      });
      return;
    }
    setCreating(true);
    setCreatedInfo(null);
    try {
      const response = await createCompany(trimmedName, trimmedKey, trimmedEmail, trimmedPassword, isActive);
      toast({
        title: "Company created",
        description: response.name,
        variant: "success",
      });
      setCreatedInfo({ id: response.id, name: response.name, key: response.enrollmentKey });
      setName("");
      setEnrollmentKey("");
      setAdminEmail("");
      setAdminPassword("");
      setIsActive(true);
      await fetchCompanies();
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        clearAuthToken();
        navigate("/login", { replace: true });
        return;
      }
      const message = err instanceof Error ? err.message : "Failed to create company";
      toast({
        title: "Create failed",
        description: message,
        variant: "destructive",
      });
    } finally {
      setCreating(false);
    }
  };

  const handleEdit = (company: Company) => {
    setEditingId(company.id);
    setEditName(company.name);
    setEditKey(company.enrollmentKey || "");
    setEditActive(company.isActive);
  };

  const handleCancel = () => {
    setEditingId(null);
    setEditName("");
    setEditKey("");
    setEditActive(true);
  };

  const handleSave = async (companyId: string) => {
    const trimmedName = editName.trim();
    const trimmedKey = editKey.trim();
    if (!trimmedName || !trimmedKey) {
      toast({
        title: "Missing fields",
        description: "Name and enrollment key are required.",
        variant: "destructive",
      });
      return;
    }

    setSavingId(companyId);
    try {
      const updated = await updateCompany(companyId, trimmedName, trimmedKey, editActive);
      setCompanies((prev) =>
        prev.map((c) => (c.id === companyId ? { ...c, ...updated } : c))
      );
      toast({
        title: "Company updated",
        description: updated.name,
        variant: "success",
      });
      handleCancel();
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        clearAuthToken();
        navigate("/login", { replace: true });
        return;
      }
      const message = err instanceof Error ? err.message : "Failed to update company";
      toast({
        title: "Update failed",
        description: message,
        variant: "destructive",
      });
    } finally {
      setSavingId(null);
    }
  };

  const handleDelete = async (companyId: string, companyName: string) => {
    if (!window.confirm(`Delete ${companyName}? This removes all related devices and data.`)) {
      return;
    }
    setSavingId(companyId);
    try {
      await deleteCompany(companyId);
      setCompanies((prev) => prev.filter((c) => c.id !== companyId));
      toast({
        title: "Company deleted",
        description: companyName,
        variant: "success",
      });
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        clearAuthToken();
        navigate("/login", { replace: true });
        return;
      }
      const message = err instanceof Error ? err.message : "Failed to delete company";
      toast({
        title: "Delete failed",
        description: message,
        variant: "destructive",
      });
    } finally {
      setSavingId(null);
    }
  };

  return (
    <div ref={containerRef} className="relative z-10 p-6 md:p-10 max-w-6xl mx-auto">
      <OrbitBackdrop className="pointer-events-none absolute -top-24 right-10 h-[260px] w-[260px] opacity-45" />
      <div data-animate="header">
        <PageHeader
        title="Companies"
        description="System-wide company overview"
        actions={
          <>
            <Button onClick={fetchCompanies} variant="outline" size="sm" disabled={loading}>
              <RefreshCw className={`h-4 w-4 mr-2 ${loading ? "animate-spin" : ""}`} />
              Refresh
            </Button>
            <UserActions />
          </>
        }
        />
      </div>

      <div className="glass-panel p-5 mb-6" data-animate="panel">
        <form onSubmit={handleCreate} className="grid gap-4 md:grid-cols-[2fr_2fr_2fr_2fr_auto] items-end">
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor="company-name">
              Company name
            </label>
            <Input
              id="company-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              placeholder="Acme Inc."
              required
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor="company-key">
              Enrollment key
            </label>
            <Input
              id="company-key"
              value={enrollmentKey}
              onChange={(event) => setEnrollmentKey(event.target.value)}
              placeholder="Enter a unique enrollment key"
              required
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor="company-admin-email">
              Admin email
            </label>
            <Input
              id="company-admin-email"
              type="email"
              value={adminEmail}
              onChange={(event) => setAdminEmail(event.target.value)}
              placeholder="admin@company.com"
              required
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor="company-admin-password">
              Admin password
            </label>
            <Input
              id="company-admin-password"
              type="password"
              value={adminPassword}
              onChange={(event) => setAdminPassword(event.target.value)}
              placeholder="Set a secure password"
              required
            />
          </div>
          <div className="flex flex-col gap-3 md:items-end">
            <div className="flex items-center gap-2">
              <Switch
                checked={isActive}
                onCheckedChange={setIsActive}
                id="company-active"
              />
              <label className="text-sm font-medium" htmlFor="company-active">
                Active
              </label>
            </div>
            <Button type="submit" disabled={creating}>
              {creating ? "Creating..." : "Create Company"}
            </Button>
          </div>
        </form>

        {createdInfo && (
          <div className="mt-4 text-sm text-muted-foreground">
            Enrollment key for{" "}
            <span className="text-foreground font-medium">{createdInfo.name}</span>:
            <div className="mt-2 flex flex-col gap-1">
              <code className="text-xs font-mono bg-muted px-2 py-1 rounded">{createdInfo.key}</code>
              <span className="text-xs">Company ID: {createdInfo.id}</span>
            </div>
          </div>
        )}
      </div>

      {loading && companies.length === 0 ? (
        <LoadingState message="Loading companies..." />
      ) : error ? (
        <ErrorMessage message={error} onRetry={fetchCompanies} />
      ) : companies.length === 0 ? (
        <EmptyState
          title="No companies found"
          description="Create a company in the database to enroll devices."
        />
      ) : (
        <div className="glass-panel overflow-hidden" data-animate="panel">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/50">
                <TableHead className="w-[220px]">Company</TableHead>
                <TableHead>Enrollment Key</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-[120px]">Devices</TableHead>
                <TableHead className="w-[260px]">Company ID</TableHead>
                <TableHead className="w-[120px] text-right">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {companies.map((company) => (
                <TableRow key={company.id}>
                  <TableCell className="font-medium">
                    {editingId === company.id ? (
                      <Input
                        value={editName}
                        onChange={(event) => setEditName(event.target.value)}
                        className="h-8"
                        disabled={savingId === company.id}
                      />
                    ) : (
                      <div className="flex items-center gap-2">
                        <Building2 className="h-4 w-4 text-muted-foreground" />
                        {company.name}
                      </div>
                    )}
                  </TableCell>
                  <TableCell>
                    {editingId === company.id ? (
                      <Input
                        value={editKey}
                        onChange={(event) => setEditKey(event.target.value)}
                        className="h-8 font-mono text-xs"
                        disabled={savingId === company.id}
                      />
                    ) : (
                      company.enrollmentKey ? (
                        <code className="text-xs font-mono bg-muted px-2 py-1 rounded">
                          {company.enrollmentKey}
                        </code>
                      ) : (
                        <span className="text-xs text-muted-foreground">—</span>
                      )
                    )}
                  </TableCell>
                  <TableCell>
                    {editingId === company.id ? (
                      <div className="flex items-center gap-2">
                        <Switch
                          checked={editActive}
                          onCheckedChange={setEditActive}
                          disabled={savingId === company.id}
                        />
                        <span className="text-xs text-muted-foreground">
                          {editActive ? "Active" : "Inactive"}
                        </span>
                      </div>
                    ) : (
                      <span className={company.isActive ? "text-success" : "text-muted-foreground"}>
                        {company.isActive ? "Active" : "Inactive"}
                      </span>
                    )}
                  </TableCell>
                  <TableCell>{company.deviceCount}</TableCell>
                  <TableCell>
                    <code className="text-xs font-mono bg-muted px-2 py-1 rounded">
                      {company.id}
                    </code>
                  </TableCell>
                  <TableCell className="text-right">
                    {editingId === company.id ? (
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleSave(company.id)}
                          disabled={savingId === company.id}
                        >
                          <Check className="h-4 w-4 text-success" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={handleCancel}
                          disabled={savingId === company.id}
                        >
                          <X className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    ) : (
                      <div className="flex items-center justify-end gap-1">
                        <Button variant="ghost" size="sm" asChild>
                          <Link to={`/companies/${company.id}/devices`}>
                            <Eye className="h-4 w-4" />
                          </Link>
                        </Button>
                        <Button variant="ghost" size="sm" onClick={() => handleEdit(company)}>
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleDelete(company.id, company.name)}
                          disabled={savingId === company.id}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
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
