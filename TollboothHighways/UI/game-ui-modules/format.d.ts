// game-ui/common/utils/format.d.ts

declare module 'game-ui/common/utils/format' {
    export interface FormatOptions {
        precision?: number;
        length?: number;
        locale?: string;
        currency?: string;
    }

    export interface NumberFormatResult {
        value: string;
        suffix?: string;
        isNegative: boolean;
        originalValue: number;
    }

    export function makePretty(input: string): string;
    export function makePrettyUppercase(input: string): string;
    export function formatInteger(value: number, options?: FormatOptions): string;
    export function formatFixedLengthInt(value: number, length: number): string;
    export function capitalize(input: string): string;
    export function formatLargeNumber(value: number, precision?: number): string;
    export function useFormattedLargeNumber(value: number, precision?: number): string;

    export const FORMAT_CONSTANTS: {
        readonly DEFAULT_PRECISION: 1;
        readonly MIN_LARGE_NUMBER_THRESHOLD: 1000;
        readonly MAX_FIXED_LENGTH: 10;
        readonly DECIMAL_PLACES: {
            readonly CURRENCY: 2;
            readonly PERCENTAGE: 1;
            readonly SCIENTIFIC: 3;
        };
    };

    export type FormattedNumber = string;
    export type FormattedText = string;
    export type FormatFunction<T extends any[], R = string> = (...args: T) => R;

    const formatUtils: {
        makePretty: typeof makePretty;
        makePrettyUppercase: typeof makePrettyUppercase;
        formatInteger: typeof formatInteger;
        formatFixedLengthInt: typeof formatFixedLengthInt;
        capitalize: typeof capitalize;
        formatLargeNumber: typeof formatLargeNumber;
        useFormattedLargeNumber: typeof useFormattedLargeNumber;
    };

    export default formatUtils;
}