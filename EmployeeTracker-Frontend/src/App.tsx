import HotToaster from "@/components/HotToaster";
import { TooltipProvider } from "@/components/ui/tooltip";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import DevicesPage from "./pages/DevicesPage";
import DeviceSummaryPage from "./pages/DeviceSummaryPage";
import CompaniesPage from "./pages/CompaniesPage";
import CompanyDevicesPage from "./pages/CompanyDevicesPage";
import LoginPage from "./pages/LoginPage";
import ProfilePage from "./pages/ProfilePage";
import NotFound from "./pages/NotFound";
import RequireAuth from "./components/RequireAuth";
import RequireSystemAdmin from "./components/RequireSystemAdmin";
import HomeRedirect from "./components/HomeRedirect";

const queryClient = new QueryClient();

const App = () => (
  <QueryClientProvider client={queryClient}>
    <TooltipProvider>
      <HotToaster />
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<HomeRedirect />} />
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/devices"
            element={
              <RequireAuth>
                <DevicesPage />
              </RequireAuth>
            }
          />
          <Route
            path="/devices/:deviceId"
            element={
              <RequireAuth>
                <DeviceSummaryPage />
              </RequireAuth>
            }
          />
          <Route
            path="/profile"
            element={
              <RequireAuth>
                <ProfilePage />
              </RequireAuth>
            }
          />
          <Route
            path="/companies"
            element={
              <RequireAuth>
                <RequireSystemAdmin>
                  <CompaniesPage />
                </RequireSystemAdmin>
              </RequireAuth>
            }
          />
          <Route
            path="/companies/:companyId/devices"
            element={
              <RequireAuth>
                <RequireSystemAdmin>
                  <CompanyDevicesPage />
                </RequireSystemAdmin>
              </RequireAuth>
            }
          />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </BrowserRouter>
    </TooltipProvider>
  </QueryClientProvider>
);

export default App;
