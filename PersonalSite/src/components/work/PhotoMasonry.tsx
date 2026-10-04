import { Children, useLayoutEffect, useState, type ReactNode } from "react";

/**
 * 与 photo-masonry.css 的列数断点一致。一两项时仍为两列。
 */
export function photoMasonryColumnCount(itemCount: number, width: number): number {
  if (itemCount <= 2) {
    return 2;
  }
  if (width <= 560) {
    return 1;
  }
  if (width <= 900) {
    return 2;
  }
  if (width >= 1400) {
    return 4;
  }
  return 3;
}

function readColumnCount(itemCount: number): number {
  if (typeof window === "undefined") {
    return itemCount <= 2 ? 2 : 3;
  }
  return photoMasonryColumnCount(itemCount, window.innerWidth);
}

type PhotoMasonryProps = {
  count: number;
  children: ReactNode;
};

/**
 * 按行分进各列。第 i 项进入第 i 列取余，从左到右即当前排序。
 */
export function PhotoMasonry({ count, children }: PhotoMasonryProps) {
  const [cols, setCols] = useState(() => readColumnCount(count));

  useLayoutEffect(() => {
    const update = () => setCols(photoMasonryColumnCount(count, window.innerWidth));
    update();
    const queries = [
      window.matchMedia("(max-width: 560px)"),
      window.matchMedia("(max-width: 900px)"),
      window.matchMedia("(min-width: 1400px)"),
    ];
    for (const query of queries) {
      query.addEventListener("change", update);
    }
    return () => {
      for (const query of queries) {
        query.removeEventListener("change", update);
      }
    };
  }, [count]);

  const items = Children.toArray(children);
  const columns: ReactNode[][] = Array.from({ length: cols }, () => []);
  items.forEach((item, index) => {
    columns[index % cols]?.push(item);
  });

  return (
    <div className="photo-masonry is-row" data-count={count}>
      {columns.map((column, index) => (
        <div className="photo-masonry-col" key={index}>
          {column}
        </div>
      ))}
    </div>
  );
}
