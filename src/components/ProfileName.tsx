import type { MouseEvent } from "react";
import { useProfileName } from "../hooks/useProfileName";

type ProfileNameProps = {
  className?: string;
};

/**
 * 可点切换的中文姓名，默认曾用名「杜宏博」。
 */
export function ProfileName({ className }: ProfileNameProps) {
  const { displayName, toggle } = useProfileName();

  function handleClick(event: MouseEvent<HTMLButtonElement>) {
    event.stopPropagation();
    toggle();
  }

  return (
    <button type="button" className={`profile-name-toggle${className ? ` ${className}` : ""}`} onClick={handleClick}>
      {displayName}
    </button>
  );
}
