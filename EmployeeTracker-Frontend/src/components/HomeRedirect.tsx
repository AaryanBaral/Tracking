import { Navigate } from "react-router-dom";
import { getAuthRole, getAuthToken } from "@/lib/auth";

export default function HomeRedirect() {
  const token = getAuthToken();
  if (!token) {
    return <Navigate to="/login" replace />;
  }

  const role = getAuthRole();
  return <Navigate to={role === "SystemAdmin" ? "/companies" : "/devices"} replace />;
}
