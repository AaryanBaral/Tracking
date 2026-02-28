import { ReactNode } from "react";
import { Navigate } from "react-router-dom";
import { getAuthRole } from "@/lib/auth";

interface RequireSystemAdminProps {
  children: ReactNode;
}

export default function RequireSystemAdmin({ children }: RequireSystemAdminProps) {
  const role = getAuthRole();

  if (role !== "SystemAdmin") {
    return <Navigate to="/devices" replace />;
  }

  return <>{children}</>;
}
