import { useEffect } from "react";

interface Props {
  message: string;
  onDismiss: () => void;
}

function friendlyMessage(raw: string): string {
  // Strip SignalR wrapper noise
  let msg = raw
    .replace(/^An unexpected error occurred invoking '.*?' on the server\.\s*/i, "")
    .replace(/^HubException:\s*/i, "")
    .replace(/^Error:\s*/i, "")
    .trim();
  // Capitalize first letter
  if (msg.length > 0) msg = msg[0].toUpperCase() + msg.slice(1);
  return msg || "Something went wrong";
}

export function Toast({ message, onDismiss }: Props) {
  useEffect(() => {
    const t = setTimeout(onDismiss, 3000);
    return () => clearTimeout(t);
  }, [message]);

  return (
    <div
      onClick={onDismiss}
      className="fixed inset-0 flex items-center justify-center z-[70] pointer-events-none"
    >
      <div className="bg-red-900/95 text-white px-6 py-4 rounded-xl text-base font-medium text-center shadow-lg pointer-events-auto cursor-pointer max-w-[80%]">
        {friendlyMessage(message)}
      </div>
    </div>
  );
}
