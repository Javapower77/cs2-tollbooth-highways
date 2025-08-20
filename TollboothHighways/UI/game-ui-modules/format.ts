// game-ui/common/utils/format.ts

import { useMemo } from 'react';

/**
 * Formats a string to make it "pretty" - removes underscores, adds proper spacing
 * @param input - The input string to format
 * @returns Formatted string with proper spacing
 */
export function makePretty(input: string): string {
    if (!input || typeof input !== 'string') return '';
    return input
        .replace(/[_-]/g, ' ')
        .replace(/([a-z])([A-Z])/g, '$1 $2')
        .replace(/\s+/g, ' ')
        .trim();
}

/**
 * Same as makePretty but converts result to uppercase
 * @param input - The input string to format
 * @returns Formatted uppercase string
 */
export function makePrettyUppercase(input: string): string {
    return makePretty(input).toUpperCase();
}

/**
 * Formats integers with thousand separators using locale formatting
 * @param value - The number to format
 * @returns Formatted integer string with separators
 */
export function formatInteger(value: number): string {
    if (typeof value !== 'number' || !Number.isFinite(value)) return '0';
    return Math.floor(value).toLocaleString();
}

/**
 * Formats integers with fixed length, padding with zeros
 * @param value - The number to format
 * @param length - The desired string length
 * @returns Zero-padded string of specified length
 */
export function formatFixedLengthInt(value: number, length: number): string {
    if (typeof value !== 'number' || !Number.isFinite(value)) {
        return '0'.padStart(length, '0');
    }
    return Math.floor(Math.abs(value)).toString().padStart(length, '0');
}

/**
 * Capitalizes the first letter of a string
 * @param input - The string to capitalize
 * @returns Capitalized string
 */
export function capitalize(input: string): string {
    if (!input || typeof input !== 'string') return '';
    return input.charAt(0).toUpperCase() + input.slice(1).toLowerCase();
}

/**
 * Formats large numbers with appropriate suffixes (K, M, B, T)
 * @param value - The number to format
 * @param precision - Number of decimal places (default: 1)
 * @returns Formatted number with suffix
 */
export function formatLargeNumber(value: number, precision: number = 1): string {
    if (typeof value !== 'number' || !Number.isFinite(value)) return '0';
    
    const absValue = Math.abs(value);
    const sign = value < 0 ? '-' : '';
    
    if (absValue < 1000) {
        return sign + Math.floor(absValue).toString();
    }
    
    const units = [
        { threshold: 1e12, suffix: 'T' }, // Trillion
        { threshold: 1e9, suffix: 'B' },  // Billion
        { threshold: 1e6, suffix: 'M' },  // Million
        { threshold: 1e3, suffix: 'K' }   // Thousand
    ];
    
    for (const unit of units) {
        if (absValue >= unit.threshold) {
            const formatted = (absValue / unit.threshold).toFixed(precision);
            return sign + formatted + unit.suffix;
        }
    }
    
    return sign + absValue.toString();
}

/**
 * React hook that returns a formatted large number with memoization
 * @param value - The number to format
 * @param precision - Number of decimal places (default: 1)
 * @returns Memoized formatted number string
 */
export function useFormattedLargeNumber(value: number, precision: number = 1): string {
    return useMemo(() => formatLargeNumber(value, precision), [value, precision]);
}

// Type definitions for better TypeScript support
export interface FormatUtils {
    makePretty: typeof makePretty;
    makePrettyUppercase: typeof makePrettyUppercase;
    formatInteger: typeof formatInteger;
    formatFixedLengthInt: typeof formatFixedLengthInt;
    capitalize: typeof capitalize;
    formatLargeNumber: typeof formatLargeNumber;
    useFormattedLargeNumber: typeof useFormattedLargeNumber;
}

// Default export object for compatibility with module loaders
const formatUtils: FormatUtils = {
    makePretty,
    makePrettyUppercase,
    formatInteger,
    formatFixedLengthInt,
    capitalize,
    formatLargeNumber,
    useFormattedLargeNumber
};

export default formatUtils;

// Additional type exports for consumer convenience
export type FormattedNumber = string;
export type FormattedText = string;

// Utility type for format function signatures
export type FormatFunction<T extends any[], R = string> = (...args: T) => R;

// Constants for common formatting scenarios
export const FORMAT_CONSTANTS = {
    DEFAULT_PRECISION: 1,
    MIN_LARGE_NUMBER_THRESHOLD: 1000,
    MAX_FIXED_LENGTH: 10,
    DECIMAL_PLACES: {
        CURRENCY: 2,
        PERCENTAGE: 1,
        SCIENTIFIC: 3
    }
} as const;

// Helper type for format constants
export type FormatConstants = typeof FORMAT_CONSTANTS;

// Re-export for convenience
export {
    makePretty as prettify,
    formatLargeNumber as compactNumber,
    useFormattedLargeNumber as useCompactNumber
};