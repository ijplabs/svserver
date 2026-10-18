import React, { useState, useEffect } from 'react';
import { ImageOff } from 'lucide-react';

interface WikiImageProps extends React.ImgHTMLAttributes<HTMLImageElement> {
  src: string;
  alt: string;
  className?: string;
  fallbackIcon?: React.ReactNode;
  fallbackText?: string;
  fallbackSrc?: string;
}

export const WikiImage: React.FC<WikiImageProps> = ({
  src,
  alt,
  className = '',
  fallbackIcon,
  fallbackText,
  fallbackSrc,
  ...props
}) => {
  const [currentSrc, setCurrentSrc] = useState<string>(src);
  const [triedFallback, setTriedFallback] = useState<boolean>(false);
  const [hasError, setHasError] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    setCurrentSrc(src);
    setTriedFallback(false);
    setHasError(false);
    setIsLoading(true);
  }, [src]);

  const handleError = () => {
    if (fallbackSrc && !triedFallback && fallbackSrc !== currentSrc) {
      setTriedFallback(true);
      setCurrentSrc(fallbackSrc);
      setIsLoading(true);
    } else {
      setHasError(true);
      setIsLoading(false);
    }
  };

  if (!currentSrc || hasError) {
    if (fallbackIcon) {
      return (
        <div className={`flex items-center justify-center bg-slate-100 dark:bg-slate-800 text-slate-400 dark:text-slate-500 rounded-lg ${className}`}>
          {fallbackIcon}
        </div>
      );
    }

    if (fallbackText) {
      const initials = fallbackText
        .split(' ')
        .map((w) => w[0])
        .filter(Boolean)
        .slice(0, 2)
        .join('')
        .toUpperCase();
      return (
        <div className={`flex items-center justify-center bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 font-semibold text-xs rounded-lg select-none ${className}`}>
          {initials || '?'}
        </div>
      );
    }

    return (
      <div className={`flex items-center justify-center bg-slate-100 dark:bg-slate-800 text-slate-400 dark:text-slate-500 rounded-lg ${className}`}>
        <ImageOff className="w-4 h-4 opacity-50" />
      </div>
    );
  }

  return (
    <div className={`relative overflow-hidden inline-flex items-center justify-center ${className}`}>
      {isLoading && (
        <div className="absolute inset-0 bg-slate-200 dark:bg-slate-800 animate-pulse rounded-lg" />
      )}
      <img
        src={currentSrc}
        alt={alt}
        referrerPolicy="no-referrer"
        loading="lazy"
        onLoad={() => setIsLoading(false)}
        onError={handleError}
        className={`w-full h-full object-contain transition-opacity duration-200 ${
          isLoading ? 'opacity-0' : 'opacity-100'
        }`}
        {...props}
      />
    </div>
  );
};
