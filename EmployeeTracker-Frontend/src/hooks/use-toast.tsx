import * as React from "react";
import { toast as hotToast } from "react-hot-toast";

type ToastVariant = "success" | "info" | "warning" | "destructive";

type ToastInput = {
  title?: React.ReactNode;
  description?: React.ReactNode;
  variant?: ToastVariant;
};

const variantStyles: Record<ToastVariant, { bar: string; text: string; bg: string }> = {
  success: {
    bar: "bg-primary",
    text: "text-foreground",
    bg: "bg-white/90",
  },
  info: {
    bar: "bg-black",
    text: "text-foreground",
    bg: "bg-white/90",
  },
  warning: {
    bar: "bg-amber-500",
    text: "text-foreground",
    bg: "bg-white/90",
  },
  destructive: {
    bar: "bg-destructive",
    text: "text-foreground",
    bg: "bg-white/90",
  },
};

function toast({ title, description, variant = "info" }: ToastInput) {
  return hotToast.custom(
    (t) => {
      const style = variantStyles[variant];
      return (
        <div
          className={`pointer-events-auto flex w-[320px] gap-3 rounded-xl border border-black/5 ${style.bg} px-4 py-3 shadow-lift backdrop-blur-md transition-all ${
            t.visible ? "translate-y-0 opacity-100" : "translate-y-3 opacity-0"
          }`}
        >
          <span className={`mt-1 h-10 w-1.5 rounded-full ${style.bar}`} />
          <div className={`flex flex-1 flex-col gap-1 ${style.text}`}>
            {title && <p className="text-sm font-semibold">{title}</p>}
            {description && <p className="text-xs text-muted-foreground">{description}</p>}
          </div>
          <button
            className="text-xs text-muted-foreground hover:text-foreground"
            onClick={() => hotToast.dismiss(t.id)}
          >
            Close
          </button>
        </div>
      );
    },
    { duration: 4500 },
  );
}

function useToast() {
  return {
    toast,
    dismiss: hotToast.dismiss,
  };
}

export { useToast, toast };
